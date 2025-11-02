// DialogueManager.cs
using System;
using UnityEngine;
using RelaxingDrive.Data;

namespace RelaxingDrive.Core
{
    /// <summary>
    /// Manages dialogue state and progression.
    /// Follows Single Responsibility Principle - only handles dialogue logic.
    /// UI observes this through events.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        // Singleton instance
        private static DialogueManager instance;
        public static DialogueManager Instance
        {
            get
            {
                if (instance == null)
                {
                    Debug.LogError("DialogueManager instance is null!");
                }
                return instance;
            }
        }
        
        // Events for UI to observe (Observer Pattern)
        public event Action<DialogueData> OnDialogueStarted;
        public event Action<string, string> OnDialogueLineChanged; // (speakerName, lineText)
        public event Action OnDialogueEnded;
        
        // Current dialogue state
        private DialogueData currentDialogue;
        private int currentLineIndex;
        private bool isDialogueActive;
        
        public bool IsDialogueActive => isDialogueActive;
        
        private void Awake()
        {
            // Singleton setup
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        /// <summary>
        /// Starts a new dialogue sequence
        /// </summary>
        public void StartDialogue(DialogueData dialogueData)
        {
            if (dialogueData == null || dialogueData.LineCount == 0)
            {
                Debug.LogWarning("Cannot start dialogue - no data or empty lines");
                return;
            }
            
            currentDialogue = dialogueData;
            currentLineIndex = 0;
            isDialogueActive = true;
            
            // Notify UI that dialogue started
            OnDialogueStarted?.Invoke(currentDialogue);
            
            // Display first line
            DisplayCurrentLine();
        }
        
        /// <summary>
        /// Advances to the next dialogue line
        /// </summary>
        public void AdvanceDialogue()
        {
            if (!isDialogueActive)
                return;
            
            currentLineIndex++;
            
            // Check if we've reached the end
            if (currentLineIndex >= currentDialogue.LineCount)
            {
                EndDialogue();
                return;
            }
            
            DisplayCurrentLine();
        }
        
        /// <summary>
        /// Ends the current dialogue
        /// </summary>
        public void EndDialogue()
        {
            isDialogueActive = false;
            currentDialogue = null;
            currentLineIndex = 0;
            
            // Notify UI that dialogue ended
            OnDialogueEnded?.Invoke();
        }
        
        /// <summary>
        /// Displays the current line to observers
        /// </summary>
        private void DisplayCurrentLine()
        {
            if (currentDialogue == null)
                return;
            
            string lineText = currentDialogue.dialogueLines[currentLineIndex];
            string speakerName = currentDialogue.speakerName;
            
            // Notify UI to update
            OnDialogueLineChanged?.Invoke(speakerName, lineText);
        }
    }
}