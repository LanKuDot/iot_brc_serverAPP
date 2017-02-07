using UnityEngine;
using System.Net.Sockets;
using System.IO;

public class ClientManager : MonoBehaviour
{
    public UnityEngine.UI.Text msgStr;
    TcpClient client;

    public void connectToServer()
    {
        client = new TcpClient( "192.168.150.94", 8000 );
        if (!client.Connected)
            msgStr.text = "Failed conencting to the server";
        else
            msgStr.text = "Conenct to server success";
    }

    public void sendMessage()
    {
        NetworkStream stream = new NetworkStream( client.Client );
        StreamWriter writer = new StreamWriter( stream );
        writer.WriteLine( "Greeting" );
        writer.Flush();
    }

    public void disconnetFromServer()
    {
        NetworkStream stream = new NetworkStream( client.Client );
        StreamWriter writer = new StreamWriter( stream );
        writer.WriteLine( "stop" );
        writer.Flush();
        client.Close();
    }
}
