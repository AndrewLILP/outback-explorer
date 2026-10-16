// File: Assets/Scripts/Core/SteamCaptureTool.cs
// ---------------------------------------------------------------------------
// SteamCaptureTool — dev-only helper for taking Steam store screenshots.
//
//   Toggle HUD      : hold ` + H     |  Gamepad: hold View/Select + Y (North)
//   Capture (2x)    : hold ` + P     |  Gamepad: hold View/Select + X (West)
//   (Avoid Ctrl+Shift combos: the Unity Editor intercepts many, e.g. Ctrl+Shift+P = Pause.)
//
// Why chords instead of bare keys?
//   Modifier combos can't be pressed by accident during play and won't clash
//   with gameplay bindings or Steam's own screenshot key (F12 / Steam+R1).
//
// Why only in Editor / Development Builds?
//   Debug.isDebugBuild is true in the Editor and in builds made with
//   "Development Build" ticked. In a release build this component removes
//   itself, so players can never trigger it. It is safe to leave in the scene.
//
// Setup: add to any GameObject in your main scene (e.g. a "DevTools" object).
// ---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class SteamCaptureTool : MonoBehaviour
{
    [Header("Capture Settings")]
    [Tooltip("Resolution multiplier. 2 on a 1920x1080 window gives 3840x2160.")]
    [Range(1, 4)]
    [SerializeField] private int superSize = 2;

    [Tooltip("Hide all UI Toolkit documents automatically for the capture frame, " +
             "even if the HUD is currently visible.")]
    [SerializeField] private bool hideHudDuringCapture = true;

    [Tooltip("Folder name for screenshots. Editor: project root. Builds: persistentDataPath.")]
    [SerializeField] private string folderName = "Screenshots";

    [Header("Exclusions")]
    [Tooltip("UIDocuments that should NEVER be hidden (rarely needed).")]
    [SerializeField] private List<UIDocument> neverHide = new List<UIDocument>();

    [Tooltip("Legacy uGUI Canvases (e.g. RCC dashboard) that should NEVER be hidden.")]
    [SerializeField] private List<Canvas> neverHideCanvases = new List<Canvas>();

    [Tooltip("Also hide World Space canvases (in-world signs/labels). Usually leave OFF.")]
    [SerializeField] private bool hideWorldSpaceCanvases = false;

    [Header("Safety")]
    [Tooltip("Leave OFF. Only tick if you deliberately want this in a release build.")]
    [SerializeField] private bool allowInReleaseBuilds = false;

    [Header("Events")]
    [Tooltip("Raised with the full file path after a screenshot is written.")]
    public UnityEvent<string> OnScreenshotSaved;

    // Input actions are built in code so this tool doesn't touch your main
    // .inputactions asset — dev tooling stays self-contained (Single Responsibility).
    private InputAction toggleHudAction;
    private InputAction captureAction;

    // Remembers each document's display value so we restore it exactly,
    // rather than forcing everything to "Flex" (some panels may start hidden).
    private readonly Dictionary<UIDocument, StyleEnum<DisplayStyle>> savedDisplay =
        new Dictionary<UIDocument, StyleEnum<DisplayStyle>>();

    // uGUI canvases we switched off, so we only re-enable the ones WE hid.
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    private bool hudHiddenByToggle;
    private bool isCapturing;

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        // Strip from release builds. Debug.isDebugBuild is always true in the Editor.
        if (!Debug.isDebugBuild && !allowInReleaseBuilds)
        {
            Destroy(this);
            return;
        }

        BuildActions();
    }

    private void OnEnable()
    {
        if (toggleHudAction == null) return; // Awake bailed out

        toggleHudAction.performed += OnToggleHud;
        captureAction.performed += OnCapture;
        toggleHudAction.Enable();
        captureAction.Enable();
    }

    private void OnDisable()
    {
        if (toggleHudAction == null) return;

        toggleHudAction.performed -= OnToggleHud;
        captureAction.performed -= OnCapture;
        toggleHudAction.Disable();
        captureAction.Disable();

        // Never leave the HUD stuck hidden if this object is disabled mid-session.
        if (hudHiddenByToggle) ShowHud();
    }

    private void OnDestroy()
    {
        toggleHudAction?.Dispose();
        captureAction?.Dispose();
    }

    // -----------------------------------------------------------------------
    // Input setup
    // -----------------------------------------------------------------------

    private void BuildActions()
    {
        // Composite bindings: the action only fires when the modifier(s) are held
        // AND the main button is pressed. "TwoModifiers" = Ctrl + Shift + key.
        // Keyboard modifier is backquote (`), NOT Ctrl+Shift — the Unity Editor
        // grabs many Ctrl+Shift combos (Ctrl+Shift+P = Editor Pause) before the game sees them.
        toggleHudAction = new InputAction("DevToggleHud", InputActionType.Button);
        toggleHudAction.AddCompositeBinding("OneModifier")
            .With("Modifier", "<Keyboard>/backquote")
            .With("Binding", "<Keyboard>/h");
        toggleHudAction.AddCompositeBinding("OneModifier")
            .With("Modifier", "<Gamepad>/select")      // View button on Xbox / Deck
            .With("Binding", "<Gamepad>/buttonNorth"); // Y on Xbox / Deck

        captureAction = new InputAction("DevCapture", InputActionType.Button);
        captureAction.AddCompositeBinding("OneModifier")
            .With("Modifier", "<Keyboard>/backquote")
            .With("Binding", "<Keyboard>/p");
        captureAction.AddCompositeBinding("OneModifier")
            .With("Modifier", "<Gamepad>/select")
            .With("Binding", "<Gamepad>/buttonWest");  // X on Xbox / Deck
    }

    // -----------------------------------------------------------------------
    // Handlers
    // -----------------------------------------------------------------------

    private void OnToggleHud(InputAction.CallbackContext _)
    {
        if (isCapturing) return;

        if (hudHiddenByToggle) ShowHud();
        else HideHud();

        hudHiddenByToggle = !hudHiddenByToggle;
        Debug.Log($"[SteamCaptureTool] HUD {(hudHiddenByToggle ? "hidden" : "shown")}");
    }

    private void OnCapture(InputAction.CallbackContext _)
    {
        if (!isCapturing) StartCoroutine(CaptureRoutine());
    }

    // -----------------------------------------------------------------------
    // Capture
    // -----------------------------------------------------------------------

    private IEnumerator CaptureRoutine()
    {
        isCapturing = true;

        // If the HUD is currently visible and we want it gone, hide it just for this shot.
        bool hidForThisShot = hideHudDuringCapture && !hudHiddenByToggle;
        if (hidForThisShot) HideHud();

        // Wait for the HUD change to actually reach the screen. UI Toolkit applies
        // style changes on its next update, so one rendered frame isn't always enough.
        yield return null;
        yield return new WaitForEndOfFrame();

        string path = BuildFilePath();

        // CaptureScreenshot writes the file at the END of the current frame,
        // re-rendering cameras at superSize x resolution.
        ScreenCapture.CaptureScreenshot(path, superSize);

        // Give it a frame to finish writing before restoring the HUD,
        // otherwise the HUD can sneak back into the shot.
        yield return new WaitForEndOfFrame();
        yield return null;

        if (hidForThisShot) ShowHud();

        Debug.Log($"[SteamCaptureTool] Saved {Screen.width * superSize}x{Screen.height * superSize} → {path}");
        OnScreenshotSaved?.Invoke(path);

        isCapturing = false;
    }

    private string BuildFilePath()
    {
        // Editor: <ProjectRoot>/Screenshots (outside Assets, so Unity won't import them).
        // Builds: persistentDataPath/Screenshots (always writable, incl. on Steam Deck).
        string root = Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            : Application.persistentDataPath;

        string folder = Path.Combine(root, folderName);
        Directory.CreateDirectory(folder); // no-op if it already exists

        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        return Path.Combine(folder, $"SundayDrive_{stamp}_{superSize}x.png");
    }

    // -----------------------------------------------------------------------
    // HUD visibility (UI Toolkit)
    // -----------------------------------------------------------------------
    // We hide via style.display rather than disabling GameObjects. Disabling would
    // break event subscriptions and singleton lifecycles (e.g. HUDController).

    private void HideHud()
    {
        savedDisplay.Clear();

        var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var doc in docs)
        {
            if (doc == null || neverHide.Contains(doc)) continue;

            VisualElement root = doc.rootVisualElement;
            if (root == null) continue;

            savedDisplay[doc] = root.style.display;
            root.style.display = DisplayStyle.None;
        }

        // --- Legacy uGUI / TextMeshPro canvases (RCC dashboard, etc.) ---
        // We search at hide-time (not in Awake) because RCC spawns the vehicle
        // and its UI at runtime. Only ROOT canvases are touched: turning off a
        // root Canvas stops all its nested canvases and TMP text rendering too.
        hiddenCanvases.Clear();

        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas == null || !canvas.isRootCanvas || !canvas.enabled) continue;
            if (neverHideCanvases.Contains(canvas)) continue;
            if (!hideWorldSpaceCanvases && canvas.renderMode == RenderMode.WorldSpace) continue;

            canvas.enabled = false; // stops rendering; scripts on it keep running
            hiddenCanvases.Add(canvas);
        }
    }

    private void ShowHud()
    {
        foreach (var pair in savedDisplay)
        {
            // The document may have been destroyed (e.g. scene change) since hiding.
            if (pair.Key == null || pair.Key.rootVisualElement == null) continue;
            pair.Key.rootVisualElement.style.display = pair.Value;
        }
        savedDisplay.Clear();

        foreach (var canvas in hiddenCanvases)
        {
            if (canvas != null) canvas.enabled = true; // may be destroyed if vehicle respawned
        }
        hiddenCanvases.Clear();
    }
}