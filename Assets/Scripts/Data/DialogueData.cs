// DialogueData.cs
using UnityEngine;

namespace RelaxingDrive.Data
{
    /// <summary>
    /// ScriptableObject that holds dialogue data for NPCs.
    /// Create instances via: Right-click → Create → Relaxing Drive → Dialogue Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "Relaxing Drive/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [Header("Speaker Info")]
        public string speakerName;
        
        [Header("Dialogue Content")]
        [TextArea(3, 10)]
        public string[] dialogueLines;
        
        [Header("Visual")]
        public Sprite speakerPortrait; // Optional: portrait image
        
        /// <summary>
        /// Get total number of dialogue lines
        /// </summary>
        public int LineCount => dialogueLines != null ? dialogueLines.Length : 0;
    }
}