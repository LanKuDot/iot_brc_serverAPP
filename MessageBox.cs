/* Handle the message displaying area.
 */
using UnityEngine;

public class MessageBox : MonoBehaviour
{
    public UnityEngine.UI.Text displayText;

    private int _numOfLines = 0;
    private bool _debugMode = false;

    void Start()
    {
        displayText.text = "";
    }

    public void debugMessage(string message)
    {
        if (!_debugMode)
            return;

        appendMessage("[DEBUG] " + message + "\n");
    }

    public void appendMessage(string message)
    {
        // If the number of lines too much, clear the content.
        if (_numOfLines > 25) {
            _numOfLines = 0;
            displayText.text = "";
        }

        if (message.Contains("\n"))
            ++_numOfLines;

        displayText.text += message;
    }
}
