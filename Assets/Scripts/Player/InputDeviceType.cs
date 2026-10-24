// InputDeviceType.cs
// Location: Assets/Scripts/Player/InputDeviceType.cs
//
// Which kind of device the player is currently using.
// PlayerInputReader decides this; UI (prompts, menus) reacts to it.

namespace RelaxingDrive.Player
{
    public enum InputDeviceType
    {
        KeyboardMouse,
        Gamepad
    }
}