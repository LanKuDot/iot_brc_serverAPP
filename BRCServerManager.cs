/* Handle the message from the BRC Client.
 */
using System;
using System.Text;
using System.Collections;
using UnityEngine;

public class BRCServerManager : MonoBehaviour
{
    public static BRCServerManager Server;

    public MessageBox messageText;
    public UnityEngine.UI.Text[] clientInfoTexts;   // Display the client info [IP aliasID]
    public UnityEngine.UI.Text broadcastMsgText;    // Holding the broadcast message in the UI
    public ParkRecorder parkRecoder;

    [HideInInspector]
    public Queue extenalMsgQueue = new Queue();

    private ClientInfo[] _clientInfos = new ClientInfo[TCPServerManager.maxClients];
    private static Color _noConnection = new Color(1.0f, 0.278f, 0.278f);
    private static Color _activeCLient = new Color(0.278f, 1.0f, 0.278f);

    void Awake()
    {
        Server = this;
    }

    void Start()
    {
        // Initialize the Clientinfos
        for (int i = 0; i < _clientInfos.Length; ++i)
            _clientInfos[i] = new ClientInfo();

        // Initialize the clientInfoTexts
        for (int i = 0; i < clientInfoTexts.Length; ++i)
            updateStatusMsg(i);

        // Initialize the Map
        MapManager.Map.loadMap();
    }

    /* Get the ID of an unused client slot.
     */
    public int getEmptyClient()
    {
        for (int i = 0; i < _clientInfos.Length; ++i)
            if (!_clientInfos[i].isVaild)
                return i;

        return -1;
    }

    /* Register a client at the specified index and IP address
     */
    public void registerClient(int index, string IPAddress)
    {
        // Invaild index, do nothing.
        if (index >= _clientInfos.Length)
            return;

        _clientInfos[index].isVaild = true;
        _clientInfos[index].IPAddress = IPAddress;

        // Update client message
        updateStatusMsg(index);
    }

    /* Update the alias ID of a BRC client.
     * @return true if is successfully updated.
     */
    bool updateClientAliasID(int index, byte newAliasID)
    {
        // Invaild ID, do nothing.
        if (index >= _clientInfos.Length ||
            !_clientInfos[index].isVaild ||
            newAliasID == 0xFF ||
            (newAliasID > 0x00 && newAliasID < 0x10))
            return false;

        // Check if there has an repeated aliasID.
        for (int i = 0; i < _clientInfos.Length; ++i) {
            if (_clientInfos[i].isVaild && (i != index) &&
                _clientInfos[i].aliasID == newAliasID)
                return false;
        }

        _clientInfos[index].aliasID = newAliasID;
        updateStatusMsg(index);
        return true;
    }

    /* Unregister a client from the server.
     */
    public void unregisterClient(int index)
    {
        // Invaild index, do nothing.
        if (index >= _clientInfos.Length)
            return;

        _clientInfos[index].isVaild = false;
        _clientInfos[index].IPAddress = "";
        _clientInfos[index].aliasID = 0xFF;

        updateStatusMsg(index);
    }

    /* Get the index of the specified client by IPAddress.
     * @return The index of the matched client.
     * @retval -1 No matched client.
     */
    public int getIndexOfClient(string IPAddress)
    {
        for (int i = 0; i < _clientInfos.Length; ++i)
            if (_clientInfos[i].isVaild &&
                IPAddress.Equals(_clientInfos[i].IPAddress))
                return i;

        return -1;
    }

    /* Get the index of the specified client by alias ID.
     * @return The index of the matched client.
     * @retval -1 No matched client.
     */
    public int getIndexOfClient(byte aliasID)
    {
        if (aliasID == 0xFF)
            return -1;

        for (int i = 0; i < _clientInfos.Length; ++i)
            if (_clientInfos[i].isVaild &&
                (_clientInfos[i].aliasID == aliasID))
                return i;

        return -1;
    }

    /* Get the alias ID of the specified client by IP address.
     */
    public byte getAliasID(string IPAddress)
    {
        byte aliasID = 0xFF;

        foreach(ClientInfo client in _clientInfos) {
            if (client.isVaild &&
                IPAddress.Equals(client.IPAddress))
                return client.aliasID;
        }

        return aliasID;
    }

    /* Get alias ID by index.
     */
    public byte getAliasID(int index)
    {
        if (index < 0 || index >= _clientInfos.Length)
            return 0xFF;

        return _clientInfos[index].aliasID;
    }

    /* Update the status message of a client.
     */
    void updateStatusMsg(int index)
    {
        if (_clientInfos[index].isVaild) {
            clientInfoTexts[index].text = index.ToString() + "    ";
            clientInfoTexts[index].text += _clientInfos[index].IPAddress;
            // Append spaces for ID alignment
            clientInfoTexts[index].text += new string(' ', 20 - _clientInfos[index].IPAddress.Length);
            if (_clientInfos[index].aliasID == 0xFF)
                clientInfoTexts[index].text += "Undefined";
            else
                clientInfoTexts[index].text += "0x" + _clientInfos[index].aliasID.ToString("X2");
            clientInfoTexts[index].color = _activeCLient;
        } else {
            clientInfoTexts[index].text = index.ToString() + "    No Connection";
            clientInfoTexts[index].color = _noConnection;
        }
    }

    /* Handle the requesting message and generate reply message.
     * @param requestFrom The index of the requesting client.
     * @param msg The raw message sent from the requesting client.
     * @param vaildBytes The vaild bytes in the _msg_.
     */
    public void handleRequest(int requestFrom, byte[] msg, int vaildBytes)
    {
        CommMsg request = CommMsgHandler.parseMessage(msg, vaildBytes);

        // Display the raw message in the form of "From ID, Message ID, to ID, message"
        messageText.appendMessage("[MSG] " + requestFrom.ToString() + ": 0x" + request.type.ToString("X2"));
        messageText.appendMessage(", 0x" + request.ID.ToString("X2"));
        if (request.buffer != null)
            messageText.appendMessage(", " + Encoding.ASCII.GetString(request.buffer) + "\n");
        else
            messageText.appendMessage("\n");

        switch (request.type) {
            // IN: MSG_REGISTER + <1 byte>aliasID
            // RE: MSG_REGISRER + <1 byte>aliasID + "OK"/"FAIL"
            case (byte)CommMsg.MsgType.REGISTER:
                if (updateClientAliasID(requestFrom, request.ID))
                    request.buffer = Encoding.ASCII.GetBytes("OK");
                else
                    request.buffer = Encoding.ASCII.GetBytes("FAIL");
                // Reply to the sender
                TCPServerManager.Server.pushToMsgQueue(requestFrom, CommMsgHandler.generateMessage(request));
                break;

            // IN: MSG_REQUEST_RFID + <4 bytes>RFID
            // RE: MSG_REQUEST_RFID + <4 bytes>RFID + <1 byte>x + <1 byte>y + <1 byte>type
            case (byte)CommMsg.MsgType.REQUEST_RFID:
                byte[] sn = new byte[4];
                // Get the serial number
                Array.Copy(request.buffer, sn, 4);

                messageText.appendMessage("[MSG/MAP] " + MapManager.Map.getDataToString(sn) + "\n");
                request.buffer = MapManager.Map.getByteData(sn);
                TCPServerManager.Server.pushToMsgQueue(requestFrom, CommMsgHandler.generateMessage(request));

                // For third fight: Find treassure.
                // For Final fight: Tag found parking point
                MapManager.Map.findTreasure(sn);

                break;

            // IN: MSG_ROUND_COMPLETE
            case (byte)CommMsg.MsgType.ROUND_COMPLETE:
                // For Final fight: Who sent round_complete.
                parkRecoder.park(_clientInfos[requestFrom].aliasID);
                // For Final fight: All cars are parked, stop the round.
                if (parkRecoder.isAllParked()) {
                    messageText.appendMessage("[INFO] All cars completed.\n");
                    RoundManager.Timer.stop();
                }
                break;

            // IN: MSG_CUSTOM + <1 byte>to-ID + Message
            // RE: MSG_CUSTOM + <1 byte>from-ID + "OK"/"FAIL"
            // OUT: MSG_CUSTOM + <1 byte>from-ID + Message
            case (byte)CommMsg.MsgType.CUSTOM: {
                int destSocketID = getIndexOfClient(request.ID);
                byte srcAliasID = _clientInfos[requestFrom].aliasID;
                byte[] customMsg = request.buffer;

                request.ID = srcAliasID;
                if (destSocketID == -1) {
                    // Try to send message to invaild destinetion ID
                    request.buffer = Encoding.ASCII.GetBytes("FAIL");
                } else {
                    request.buffer = Encoding.ASCII.GetBytes("OK");
                }

                // Reply to the sender
                TCPServerManager.Server.pushToMsgQueue(requestFrom, CommMsgHandler.generateMessage(request));

                // Send to the receiver
                if (destSocketID != -1) {
                    request.buffer = customMsg;
                    TCPServerManager.Server.pushToMsgQueue(destSocketID, CommMsgHandler.generateMessage(request));
                }
                break;
            }

            // IN: MSG_CUSTOM_BROADCAST + Message
            // RE: MSG_CUSTOM + <1 byte>from-ID + "OK"
            // OUT: MSG_CUSTOM + <1 byte>from-ID + Message
            case (byte)CommMsg.MsgType.CUSTOM_BROADCAST: {
                byte srcAliasID = _clientInfos[requestFrom].aliasID;
                byte[] customMsg = request.buffer;

                request.ID = srcAliasID;
                request.buffer = Encoding.ASCII.GetBytes("OK");

                // Reply to the sender
                TCPServerManager.Server.pushToMsgQueue(requestFrom, CommMsgHandler.generateMessage(request));

                // Broadcast to each vaild client
                request.buffer = customMsg;
                for (int i = 0; i < _clientInfos.Length; ++i)
                    if (_clientInfos[i].isVaild && i != requestFrom)
                        TCPServerManager.Server.pushToMsgQueue(i, CommMsgHandler.generateMessage(request));
                break;
            }
        }
    }

    /* Broadcast a MSG_ROUND_START message to all the active client
     * Initialize the map and the parking list
     */
    public void startRound()
    {
        // Initialize the treasure map
        MapManager.Map.initialTreasureMap();

        // Initialize the parking list
        int targetCars;
        byte[] aliasIDs = new byte[TCPServerManager.maxClients];
        for (int i = 0; i < _clientInfos.Length; ++i)
            aliasIDs[i] = _clientInfos[i].aliasID;
        targetCars = parkRecoder.initialParkWho(aliasIDs);
        messageText.appendMessage("[INFO] There are " + targetCars.ToString() + " cars to park.\n");

        // Broadcast the MSG_ROUND_START
        CommMsg newMsg = new CommMsg();
        newMsg.type = (byte)CommMsg.MsgType.ROUND_START;

        for (int i = 0; i < _clientInfos.Length; ++i)
            if (_clientInfos[i].isVaild)
                TCPServerManager.Server.pushToMsgQueue(i, CommMsgHandler.generateMessage(newMsg));

        TCPServerManager.Server.forceSendingMsg();
        messageText.appendMessage("[INFO] Round started\n");
    }

    /* Broadcast a MSG_ROUND_END message to all the active client.
     */
    public void stopRound()
    {
        CommMsg newMsg = new CommMsg();
        newMsg.type = (byte)CommMsg.MsgType.ROUND_END;

        for (int i = 0; i < _clientInfos.Length; ++i)
            if (_clientInfos[i].isVaild)
                TCPServerManager.Server.pushToMsgQueue(i, CommMsgHandler.generateMessage(newMsg));

        TCPServerManager.Server.forceSendingMsg();
        messageText.appendMessage("[INFO] Round stopped\n");
    }

    /* The callback function of the broadcast button.
     */
    public void activeBroadcast()
    {
        CommMsg newMsg = new CommMsg();
        newMsg.type = (byte)CommMsg.MsgType.CUSTOM_BROADCAST;
        newMsg.ID = 0x01;   // Active broadcast from the server
        newMsg.buffer = Encoding.ASCII.GetBytes(broadcastMsgText.text);

        messageText.appendMessage("[INFO] Broadcast: " + broadcastMsgText.text + "\n");

        for (int i = 0; i < _clientInfos.Length; ++i)
            if (_clientInfos[i].isVaild)
                TCPServerManager.Server.pushToMsgQueue(i, CommMsgHandler.generateMessage(newMsg));

        TCPServerManager.Server.forceSendingMsg();
    }
}

/* The data structure for storing the information of clients
 */
class ClientInfo
{
    public byte aliasID;
    public string IPAddress;
    public bool isVaild;

    public ClientInfo()
    {
        aliasID = 0xFF;
        IPAddress = "";
        isVaild = false;
    }
}