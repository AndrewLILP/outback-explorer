// SimpleInputTest.cs
using UnityEngine;

/// <summary>
/// STANDALONE INPUT TESTER
/// Attach this to ANY GameObject to test if Unity is receiving input.
/// This runs independently of all other systems.
/// DELETE AFTER TESTING!
/// </summary>
public class SimpleInputTest : MonoBehaviour
{
    void Update()
    {
        // Test Input.GetAxis (old Input Manager)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (h != 0 || v != 0)
        {
            Debug.Log($"📊 [SimpleInputTest] GetAxis → H: {h:F3}, V: {v:F3}");
        }

        // Test direct key detection (bypasses Input Manager)
        if (Input.GetKey(KeyCode.W))
        {
            Debug.Log("⌨️ [SimpleInputTest] W key is DOWN");
        }
        if (Input.GetKey(KeyCode.A))
        {
            Debug.Log("⌨️ [SimpleInputTest] A key is DOWN");
        }
        if (Input.GetKey(KeyCode.S))
        {
            Debug.Log("⌨️ [SimpleInputTest] S key is DOWN");
        }
        if (Input.GetKey(KeyCode.D))
        {
            Debug.Log("⌨️ [SimpleInputTest] D key is DOWN");
        }

        // Test E key
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("⌨️ [SimpleInputTest] E key PRESSED");
        }

        // Test arrow keys
        if (Input.GetKey(KeyCode.UpArrow))
        {
            Debug.Log("⌨️ [SimpleInputTest] UP ARROW is DOWN");
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            Debug.Log("⌨️ [SimpleInputTest] DOWN ARROW is DOWN");
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            Debug.Log("⌨️ [SimpleInputTest] LEFT ARROW is DOWN");
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            Debug.Log("⌨️ [SimpleInputTest] RIGHT ARROW is DOWN");
        }
    }
}