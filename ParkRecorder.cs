/* Record and display who needs to park and whether it has parked or not.
 */
using System.Collections.Generic;
using UnityEngine;

public class ParkRecorder : MonoBehaviour
{
    public UnityEngine.UI.Text[] parkingStatus;

    private byte[] _indexMap;       // The searching table, mapping car ID to index.
    private bool[] _isParkedMap;    // Mark whether the car parked or not
    private int _totalParkCount = 0;
    private int _parkedCount = 0;

    private static Color _unParked = new Color(1.0f, 0.278f, 0.278f);
    private static Color _parked = new Color(0.278f, 1.0f, 0.278f);

    void Start()
    {
        // Clear the existing text.
        for (int i = 0; i < parkingStatus.Length; ++i)
            parkingStatus[i].text = "";
    }

    /* Initialize who needs to park.
     */
    public int initialParkWho(byte[] whos)
    {
        // Only record vaild IDs
        List<byte> vaildIDs = new List<byte>();
        foreach (byte who in whos)
            if (who != 0xFF)
                vaildIDs.Add(who);

        // Create and initialize the _indexMap and _isParkedMap
        _indexMap = new byte[vaildIDs.Count];
        _isParkedMap = new bool[vaildIDs.Count];
        for (int i = 0; i < vaildIDs.Count; ++i) {
            _indexMap[i] = vaildIDs[i];
            _isParkedMap[i] = false;
        }

        reset();
        return _indexMap.Length;
    }

    /* Reset all cars to unparked.
     */
    void reset()
    {
        int i = 0;
        for (; i < _indexMap.Length; ++i) {
            parkingStatus[i].text = "0x" + _indexMap[i].ToString("X2");
            parkingStatus[i].color = _unParked;
        }
        for (; i < parkingStatus.Length; ++i)
            parkingStatus[i].text = "";

        _totalParkCount = _indexMap.Length;
        _parkedCount = 0;
    }

    /* Set the car specified to parked.
     */ 
    public void park(byte who)
    {
        for (int i = 0; i < _indexMap.Length; ++i)
            if (_indexMap[i] == who && !_isParkedMap[i]) {
                _isParkedMap[i] = true;
                parkingStatus[i].color = _parked;
                ++_parkedCount;
                break;
            }
    }

    /* Check if all cars have parked.
     */
    public bool isAllParked()
    {
        if (_parkedCount == _totalParkCount)
            return true;

        return false;
    }
}