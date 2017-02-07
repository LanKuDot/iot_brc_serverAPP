/* Manage the mapping of RFID serial numbers.
 * Store the coordination and map type of the corresponding RFID SN.
 */
using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Runtime.Serialization.Formatters.Binary;

public class MapManager : MonoBehaviour
{
    public static MapManager Map;

    public MessageBox messageText;
    public UnityEngine.UI.Text[] treasureStatus;

    // Use serial number as the key and MapData as the value
    private Hashtable _map = new Hashtable(new ByteArrayComparer());
    // The vaule of the _treasureMap marks if the treasure is found or not.
    // Use serial number as the key.
    private Hashtable _treasureMap = new Hashtable(new ByteArrayComparer());
    private int _totalTreasures = 0;
    private int _foundTreasures = 0;

    private static Color _colorNotFound = new Color(1.0f, 0.278f, 0.278f, 0.0f);
    private static Color _colorFound = new Color(0.278f, 1.0f, 0.278f);

    void Awake()
    {
        Map = this;
    }

    /* Check if the map file is existing and try to load the file.
     */
    public void loadMap()
    {
        if (!Directory.Exists(Application.persistentDataPath))
            Directory.CreateDirectory(Application.persistentDataPath);

        string[] filelist = Directory.GetFiles(Application.persistentDataPath, "*.dat");
        if (filelist.Length == 0) {
            messageText.appendMessage("[ERROR] Please put the mapping file at ");
            messageText.appendMessage(Application.persistentDataPath + "/map.dat\n");
        } else {
            loadMapFromFile(filelist[0]);
            messageText.appendMessage("[INFO] Map file loaded\n");
            dislpayTreasureInfo();
        }
    }

    /* Load the map from the file.
     */
    void loadMapFromFile(string filepath)
    {
        FileStream inFile = File.Open(filepath, FileMode.Open);
        BinaryFormatter bf = new BinaryFormatter();
        string[] mapLines = bf.Deserialize(inFile) as string[];

        // Convert the input data to the mapping information
        foreach (string mapInfo in mapLines) {
            // Invaild data
            if (mapInfo.Length < 19 || mapInfo.IndexOf('#') == 0)
                continue;

            // [0..7] Extract the 4-byte serial number
            byte[] sn = new byte[4];
            sn[0] = Convert.ToByte(mapInfo.Substring(0, 2), 16);
            sn[1] = Convert.ToByte(mapInfo.Substring(2, 2), 16);
            sn[2] = Convert.ToByte(mapInfo.Substring(4, 2), 16);
            sn[3] = Convert.ToByte(mapInfo.Substring(6, 2), 16);

            // [9..10][12..13] Extract the coordination
            byte x = Convert.ToByte(mapInfo.Substring(9, 2), 10);
            byte y = Convert.ToByte(mapInfo.Substring(12, 2), 10);

            // [17..18] Extract the type
            byte type = Convert.ToByte(mapInfo.Substring(17, 2), 16);

            // Push to the mapping pool
            _map.Add(sn, new MapData(sn, x, y, type));

            // Also push to the treasure pool
            // For final fight: add parking point
            if (type < 0x25 && type > 0x20) {
                byte[] coordinate = { x, y };
                if (!_treasureMap.ContainsKey(coordinate)) {
                    _treasureMap.Add(coordinate, false);
                    ++_totalTreasures;
                }
            }
        }

        initialTreasureMap();
    }

    /* Clear the existing map.
     */
    void clearMap()
    {
        _map.Clear();
        _treasureMap.Clear();
    }

    /* Debug function: List all the map data in the _map.
     */
    void listAllData()
    {
        if (_map.Count == 0)
            return;

        foreach (object key in _map.Keys) {
            MapData data = (MapData)_map[key];
            messageText.appendMessage(data.ToString() + "\n");
        }
    }

    MapData getData(byte[] sn)
    {
        MapData requestData = null;

        if (_map.ContainsKey(sn)) {
            requestData = (MapData)_map[sn];
        } else {
            requestData = new MapData(sn, 0xFF, 0xFF, (byte)MapData.Type.INVAILD);
        }

        return requestData;
    }

    /* Get the requested mapping data in bytes.
     * [0..3] sn [4] x [5] y [6] type
     */
    public byte[] getByteData(byte[] sn)
    {
        MapData requestData = getData(sn);
        byte[] b = new byte[7];

        b[0] = requestData.serialNumber[0];
        b[1] = requestData.serialNumber[1];
        b[2] = requestData.serialNumber[2];
        b[3] = requestData.serialNumber[3];
        b[4] = requestData.x;
        b[5] = requestData.y;
        b[6] = requestData.type;

        return b;
    }

    /* Get the debugging string of the reqeusted MapData.
     */
    public string getDataToString(byte[] sn)
    {
        MapData requestData = getData(sn);
        return requestData.ToString();
    }

    /* Display the information of treasures to the screen.
     */
    void dislpayTreasureInfo()
    {
        if (_totalTreasures < 1) {
            messageText.appendMessage("[INFO] Uh, No parking points in this map.\n");
            return;
        }

        messageText.appendMessage("[INFO] There are " + _totalTreasures.ToString() + " parking points.\n");
        //foreach (object key in _treasureMap.Keys) {
        //    byte[] b = key as byte[];
        //    messageText.appendMessage("    (");
        //    messageText.appendMessage(b[0].ToString("D2") + ", ");
        //    messageText.appendMessage(b[1].ToString("D2") + ")\n");
        //}
    }

    /* Set all the treasures to unfound.
     */
    public void initialTreasureMap()
    {
        if (_totalTreasures < 1)
            return;

        _foundTreasures = 0;

        int i = 0;

        // Reset the status and its status text
        ArrayList keys = new ArrayList();
        foreach (object key in _treasureMap.Keys)
            keys.Add((byte[])key);
        foreach (byte[] key in keys) {
            _treasureMap[key] = false;
            treasureStatus[i].text = "(" + key[0].ToString("X2") + "," + key[1].ToString("X2") + ")";
            treasureStatus[i++].color = _colorNotFound;
        }
    }

    /* Check if the requesting serial number is a treasure.
     */
    public void findTreasure(byte[] sn)
    {
        MapData mapData = getData(sn);
        byte[] coordinate = { mapData.x, mapData.y };
        if (_treasureMap.ContainsKey(coordinate) && !(bool)_treasureMap[coordinate]) {
            _treasureMap[coordinate] = true;
            ++_foundTreasures;

            // Update the treasure status text
            string coordinateInStr = "(" + mapData.x.ToString("X2") + "," + mapData.y.ToString("X2") + ")";
            foreach (UnityEngine.UI.Text statusText in treasureStatus) {
                if (statusText.text.Contains(coordinateInStr)) {
                    statusText.color = _colorFound;
                    break;
                }
            }
        }
    }

    public bool isAllTreasureFound()
    {
        if (_foundTreasures == _totalTreasures)
            return true;

        return false;
    }
}

class MapData
{
    public enum Type : byte
    {
        NORMAL = 0x01,
        TREASURE = 0x02,
        PARK_1 = 0x21,
        PARK_2 = 0x22,
        PARK_3 = 0x23,
        PARK_4 = 0x24,
        INVAILD = 0xFF,
    };

    public byte[] serialNumber;
    public byte x;
    public byte y;
    public byte type;

    public MapData(byte[] sn, byte x, byte y, byte type)
    {
        serialNumber = sn;
        this.x = x;
        this.y = y;
        this.type = type;
    }

    public override string ToString()
    {
        string s = "";

        for (int i = 0; i < 4; ++i)
            s += serialNumber[i].ToString("X2");
        s += (", (" + x.ToString() + "," + y.ToString() + "), ");
        s += ("0x" + type.ToString("X2"));

        return s;
    }
}

class ByteArrayComparer : IEqualityComparer
{
    public int GetHashCode(object obj)
    {
        byte[] arr = obj as byte[];
        int hash = 0;

        foreach (byte b in arr)
            hash ^= b;

        return hash;
    }

    public new bool Equals(object x, object y)
    {
        byte[] arr1 = x as byte[];
        byte[] arr2 = y as byte[];

        if (arr1.Length != arr2.Length)
            return false;

        for (int i = 0; i < arr1.Length; ++i)
            if (arr1[i] != arr2[i])
                return false;

        return true;
    }
}