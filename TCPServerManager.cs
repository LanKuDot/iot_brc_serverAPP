/* Main handler which transcieves messages with the client.
 */
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class TCPServerManager : MonoBehaviour
{
    public static TCPServerManager Server;

    public UnityEngine.UI.Text serverTriggerBtnText;
    public MessageBox messageText;

    // The main thread may access the message queue.
    public Mutex msgQueueMutex = new Mutex();

    public static int maxClients = 5; // At most 5 clients can connect to the server.

    private volatile bool _keepRunning = false;    // The flag to make server run.
    private volatile bool _forceSendingPending = false;
    private Thread _serverThread;
    // The mapping of the socket to the client would be stored at the BRCServer.
    private Socket[] _clientSockets = new Socket[maxClients];
    private Queue _sendingMsgQueue = new Queue();
    private ArrayList _socketList = new ArrayList();

    void Awake()
    {
        Server = this;
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Escape)) {
            if (_keepRunning)
                StopServer();
            Application.Quit();
        }
    }

    /* The callback function for the button event which toggle server on and off.
     */
    public void ToggleServer()
    {
        if (!_keepRunning)
            StartListening();
        else if (_serverThread.IsAlive)
            StopServer();
    }

    /* Collect the information for the TCP server and
     * create a thread for running server.
     */
    void StartListening()
    {
        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress serverIP = null;

        // Extract the ip address containing 192.168.x.x
        for (int i = 0; i < ipHostInfo.AddressList.Length; ++i) {
            string ipStr = ipHostInfo.AddressList[i].ToString();
            if (ipStr.Contains("192.168")) {
                serverIP = ipHostInfo.AddressList[i];
                break;
            }
        }

        // If there is no vaild local IP found, tell the user and stop the application.
        if (serverIP == null) {
            messageText.appendMessage("[ERROR] No vaild IP found. Do you active the AP?\n");
            return;
        }

        messageText.appendMessage("[INFO] Start server on " + serverIP.ToString() + ":5000\n");
        serverTriggerBtnText.text = "Stop Server";

        // Start server thread
        _serverThread = new Thread(() => ServerListening(serverIP, 5000));
        _serverThread.Start();
    }

    /* The main loop of listening the incoming request.
     */
    void ServerListening(IPAddress serverIP, int port)
    {
        // Create server end point and socket
        IPEndPoint serverEndPoint = new IPEndPoint(serverIP, 5000);
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        byte[] msg = null;
        int bytesReceived = 0;

        try {
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Bind(serverEndPoint);
            listener.Listen(maxClients);

            _keepRunning = true;
            messageText.appendMessage("[INFO] Server started\n");

            // Create the selecting list
            _socketList.Add(listener);   // Server socket always be the first one

            while (_keepRunning) {
                ArrayList checkingList = (ArrayList)_socketList.Clone();
                Socket.Select(checkingList, null, null, 100);

                // Check if there is any available message.
                if (checkingList.Count != 0) {
                    // Handle each vaild socket.
                    foreach (Socket activeSocket in checkingList) {
                        // Server sokect got a new connection
                        if (activeSocket.Equals(listener)) {
                            // Get the ID of unused slot.
                            // TODO Handle the situation of invaild ID.
                            Socket newSocket = listener.Accept();
                            string socketIP = getSocketIP(newSocket);
                            int i = BRCServerManager.Server.getIndexOfClient(socketIP);

                            // Check if it's the old connection.
                            if (i >= 0) {
                                // Close the old one.
                                closeConnection(i);
                                messageText.appendMessage("[INFO] Client " + socketIP + " reconnected\n");
                            } else {
                                messageText.appendMessage("[INFO] Client " + socketIP + " connected\n");
                            }

                            // Register the new client
                            i = BRCServerManager.Server.getEmptyClient();
                            _clientSockets[i] = newSocket;
                            BRCServerManager.Server.registerClient(i, socketIP);

                            // Trace the new client
                            _socketList.Add(newSocket);
                        } 
                        // Receive message from the client
                        else {
                            msg = new byte[32];
                            bytesReceived = receiveHandler(activeSocket, ref msg);

                            // The connection closed accidentally
                            if (bytesReceived < 0) {
                                return;
                            }

                            // Get the ID of source socket
                            string socketIP = getSocketIP(activeSocket);
                            int requsetSrc = BRCServerManager.Server.getIndexOfClient(socketIP);

                            if (bytesReceived > 0) {
                                BRCServerManager.Server.handleRequest(requsetSrc, msg, bytesReceived);

                                // There are something to be sent after the request.
                                sendMsg();
                            }
                            // Client closed
                            else {
                                // Message
                                messageText.appendMessage("[INFO] Client " + socketIP + " disconnected\n");
                                closeConnection(requsetSrc);
                            }
                        }
                    }
                }

                if (_forceSendingPending) {
                    sendMsg();
                    _forceSendingPending = false;
                }
            }
        } catch (Exception e) {
            messageText.appendMessage(e.ToString());
            messageText.appendMessage("\nException occured.\n");
            messageText.appendMessage("Report the problem and restart the APP.\n");
        }

        listener.Close();
    }

    void StopServer()
    {
        _keepRunning = false;
        _serverThread.Join();

        serverTriggerBtnText.text = "Start Server";
        messageText.appendMessage("[INFO] Server Stopped\n");
    }

    /* Remove the target socket from the tracing list and
     * unregister its client from the BRC server.
     */
    void closeConnection(int targetID)
    {
        // Remove the closed socket from the socket list
        _socketList.Remove(_clientSockets[targetID]);
        _clientSockets[targetID].Shutdown(SocketShutdown.Both);
        _clientSockets[targetID].Close();
        _clientSockets[targetID] = null;

        // Clear the client information
        BRCServerManager.Server.unregisterClient(targetID);

        // Debug Message
        messageText.debugMessage("_socketList.count: " + _socketList.Count.ToString());

        // Status of _clientSockets
        string msg = "Vaild _clientSockets: ";

        for (int i = 0; i < _clientSockets.Length; ++i)
            if (_clientSockets[i] != null)
                msg += (i.ToString() + " ");

        messageText.debugMessage(msg);
    }

    /* Handling the receving event.
     */
    int receiveHandler(Socket targetSocket, ref byte[] buf)
    {
        int bytesReveiced = 0;

        try {
            SocketError error;

            bytesReveiced = targetSocket.Receive(buf, 0, 32, SocketFlags.None, out error);

            if (error != SocketError.Success) {
                throw new Exception("SocketError " + error.ToString());
            }
        } catch (Exception e) {
            // Exception occurred, close the connection forcedly
            byte aliasID = BRCServerManager.Server.getAliasID(getSocketIndex(targetSocket));
            messageText.appendMessage("[ERROR] " + e.Message + ": when receving message from ");
            messageText.appendMessage("0x" + aliasID.ToString("X2") + ".\n");

            messageText.appendMessage("[WARN] Connection closed forcedly\n");
            closeConnection(getSocketIndex(targetSocket));

            bytesReveiced = -1;
        }

        return bytesReveiced;
    }

    /* Handling the sending event. If the method if failed, the caller will
     * close the connection.
     */
    int sendHandler(Socket targetSocket, byte[] msgBuf, int msgLen)
    {
        int byteSent = 0;

        try {
            SocketError error;

            // Try to send message to client.
            byteSent = targetSocket.Send(msgBuf, 0, msgLen, SocketFlags.None, out error);

            if (error != SocketError.Success) {
                throw new Exception("SocketError " + error.ToString());
            }
        } catch (Exception e) {
            // FIXME Server still freezed when encountered the connection reset of the client socket.
            byte aliasID = BRCServerManager.Server.getAliasID(getSocketIndex(targetSocket));
            messageText.appendMessage("[ERROR] " + e.Message + ": when sending message to ");
            messageText.appendMessage("0x" + aliasID.ToString("X2") + ".\n");

            messageText.appendMessage("[WARN] Connection closed forcedly\n");
            closeConnection(getSocketIndex(targetSocket));

            byteSent = -1;
        }

        return byteSent;
    }

    /* Get the IP address of the specified socket in string.
     */
    string getSocketIP(Socket socket)
    {
        IPEndPoint endPoint = socket.RemoteEndPoint as IPEndPoint;
        return endPoint.Address.ToString();
    }

    int getSocketIndex(Socket socket)
    {
        for (int i = 0; i < _clientSockets.Length; ++i)
            if (socket.Equals(_clientSockets[i]))
                return i;
        return -1;
    }

    int getByteLen(byte[] b)
    {
        int byteLen = 0;

        // Start from the end of the byte array.
        while (byteLen < b.Length && b[byteLen] != 0)
            ++byteLen;

        return byteLen;
    }

    /* Get the message length from index of _from_.
     * The total length will be at least _from_ bytes.
     */
    int getByteLen(byte[] b, int from)
    {
        int byteLen = from;

        while (byteLen < b.Length && b[byteLen] != 0)
            ++byteLen;

        return byteLen;
    }

    /* Push a message item to the sending message queue.
     */
    public void pushToMsgQueue(int destSocketID, byte[] msg)
    {
        MessageItem newItem = new MessageItem(destSocketID, msg);
        msgQueueMutex.WaitOne();
        _sendingMsgQueue.Enqueue(newItem);
        msgQueueMutex.ReleaseMutex();
    }

    void sendMsg()
    {
        msgQueueMutex.WaitOne();
        while (_sendingMsgQueue.Count != 0) {
            MessageItem msgItem = (MessageItem)_sendingMsgQueue.Dequeue();
            int msgLen = 0;

            // The first 7 bytes of MSG_REQUEST_RFID always be vaild.
            // To avoid treating the coordination 0 as null character in getByteLen().
            if (msgItem.message[0] == (byte)CommMsg.MsgType.REQUEST_RFID)
                msgLen = getByteLen(msgItem.message, 7);
            else
                msgLen = getByteLen(msgItem.message);

            if (_clientSockets[msgItem.destSocketID] != null)
                sendHandler(_clientSockets[msgItem.destSocketID], msgItem.message, msgLen);
        }
        msgQueueMutex.ReleaseMutex();
    }

    /* Force the TCPServerManager sending the message in the _sendingMsgQueue
     */
    public void forceSendingMsg()
    {
        _forceSendingPending = true;
    }
}

/* The data structure for a message item in the sending queue.
 */
public class MessageItem
{
    public int destSocketID;
    public byte[] message;

    public MessageItem(int destSocketID, byte[] msg)
    {
        this.destSocketID = destSocketID;
        message = msg;
    }
}