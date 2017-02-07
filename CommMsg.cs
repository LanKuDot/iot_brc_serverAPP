/* Define the data struture for communicating between server and client.
 * Provide the convertor which converts the raw message to the readable message,
 * and vice versa.
 */
using System;

/* The data structure of readable message.
 */
public class CommMsg
{
    /* The available types of the message
     */
    public enum MsgType : byte
    {
        REGISTER = 0x01,
        REQUEST_RFID = 0x10,
        ROUND_COMPLETE = 0x11,
        ROUND_START = 0x20,
        ROUND_END = 0x21,
        CUSTOM = 0x70,
        CUSTOM_BROADCAST = 0x71,
        INVAILD = 0xFF
    };

    public enum MsgID : byte
    {
        SERVER = 0x01
    };

    public byte type;       // The type of the message
    public byte ID;         // The ID of the sender or receiver
    public byte[] buffer;   // The message buffer

    public CommMsg()
    {
        type = (byte)MsgType.INVAILD;
        ID = 0xFF;
        buffer = null;
    }
}

/* The convertor of the communication message.
 */
public class CommMsgHandler
{
    /* Convert the raw message to the readable messge.
     */
    public static CommMsg parseMessage(byte[] msg, int vaildBytes)
    {
        CommMsg newMsg = new CommMsg();

        // The message type is at the first byte.
        newMsg.type = msg[0];

        // Parse the message accroding to the message type.
        switch (msg[0]) {
            // REGISTER <ID>
            case (byte)CommMsg.MsgType.REGISTER:
                newMsg.ID = msg[1]; // New alias ID
                break;

            // REQUEST_RFID <RFID SN>
            case (byte)CommMsg.MsgType.REQUEST_RFID:
                // 4-byte RFID serial number
                newMsg.buffer = new byte[4];
                Buffer.BlockCopy(msg, 1, newMsg.buffer, 0, newMsg.buffer.Length);
                break;

            // ROUND_COMPLETE
            case (byte)CommMsg.MsgType.ROUND_COMPLETE:
                // No additional message
                break;

            // CUSTOM <to ID> <Message>
            case (byte)CommMsg.MsgType.CUSTOM:
                newMsg.ID = msg[1]; // Destination ID
                newMsg.buffer = new byte[vaildBytes - 2];
                Buffer.BlockCopy(msg, 2, newMsg.buffer, 0, newMsg.buffer.Length);
                break;

            // CUSTOM_BROADCAST <message>
            case (byte)CommMsg.MsgType.CUSTOM_BROADCAST:
                newMsg.buffer = new byte[vaildBytes - 1];
                Buffer.BlockCopy(msg, 1, newMsg.buffer, 0, newMsg.buffer.Length);
                break;

            default:
                newMsg.type = (byte)CommMsg.MsgType.INVAILD;
                break;
        }

        return newMsg;
    }

    /* Convert the readable message to the raw message.
     */
    public static byte[] generateMessage(CommMsg msg)
    {
        byte[] newMsg = null;

        if (msg.buffer != null)
            newMsg = new byte[msg.buffer.Length + 2];
        else
            newMsg = new byte[2];

        // The message type if at the first byte.
        newMsg[0] = msg.type;

        // Generate the message accroding to the message type.
        switch (msg.type) {
            case (byte)CommMsg.MsgType.REGISTER:
            case (byte)CommMsg.MsgType.CUSTOM:
            case (byte)CommMsg.MsgType.CUSTOM_BROADCAST:
                newMsg[1] = msg.ID;
                Buffer.BlockCopy(msg.buffer, 0, newMsg, 2, msg.buffer.Length);
                break;

            case (byte)CommMsg.MsgType.REQUEST_RFID:
                Buffer.BlockCopy(msg.buffer, 0, newMsg, 1, msg.buffer.Length);
                break;

            case (byte)CommMsg.MsgType.ROUND_START:
            case (byte)CommMsg.MsgType.ROUND_END:
                // The message is from the server.
                newMsg[1] = (byte)CommMsg.MsgID.SERVER;
                break;
        }

        return newMsg;
    }
}