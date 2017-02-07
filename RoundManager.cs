/* Manage the timer.
 */
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Timer;

    public UnityEngine.UI.Text btnText;
    public UnityEngine.UI.Text timerText;

    private bool _counting = false;
    private float _startTime = 0.0f;
    private float _curTime = 0.0f;

    void Awake()
    {
        Timer = this;
    }

    void Update()
    {
        if (!_counting)
            return;

        _curTime = Time.fixedTime - _startTime;
        timerText.text = _curTime.ToString("F1") + " sec";
    }

    public void triggerRound()
    {
        if (_counting) {
            btnText.text = "Start Round";
            BRCServerManager.Server.stopRound();
        } else {
            // Send the round starting message and begin timing
            BRCServerManager.Server.startRound();
            btnText.text = "Stop Round";
            _startTime = Time.fixedTime;
        }

        _counting = !_counting;
    }

    /* Stop the round not trigger.
     */
    public void stop()
    {
        if (_counting)
            triggerRound();
    }
}
