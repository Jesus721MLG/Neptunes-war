using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP-based game client that connects to a GameServer,
/// sends NetworkMessage objects, and receives incoming messages.
/// </summary>
public class GameClient : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private string serverIP = "127.0.0.1";
    [SerializeField] private int serverPort = 7777;

    private TcpClient client;
    private NetworkStream stream;
    private StreamReader reader;
    private Thread receiveThread;
    private volatile bool isConnected;

    /// <summary>
    /// Event raised on the background thread when a message is received from the server.
    /// Subscribers should dispatch to the main thread if needed.
    /// </summary>
    public event Action<NetworkMessage> OnMessageReceived;

    /// <summary>Whether the client is currently connected to the server.</summary>
    public bool IsConnected => isConnected;

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    /// <summary>
    /// Connects to the game server at the configured IP and port.
    /// </summary>
    public void ConnectToServer()
    {
        ConnectToServer(serverIP, serverPort);
    }

    /// <summary>
    /// Connects to the game server at the specified IP and port.
    /// </summary>
    /// <param name="ip">The server IP address.</param>
    /// <param name="port">The server port.</param>
    public void ConnectToServer(string ip, int port)
    {
        try
        {
            client = new TcpClient();
            client.Connect(ip, port);
            stream = client.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            isConnected = true;

            receiveThread = new Thread(ReceiveMessages)
            {
                IsBackground = true
            };
            receiveThread.Start();

            Debug.Log($"[GameClient] Connected to server at {ip}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] Failed to connect to {ip}:{port} — {e.Message}");
            isConnected = false;
        }
    }

    /// <summary>
    /// Sends a NetworkMessage to the server.
    /// </summary>
    /// <param name="message">The message to send.</param>
    public void SendMessage(NetworkMessage message)
    {
        if (!isConnected || client == null || !client.Connected)
        {
            Debug.LogWarning("[GameClient] Cannot send message: not connected.");
            return;
        }

        try
        {
            string json = message.ToJson() + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
            stream.Flush();

            Debug.Log($"[GameClient] Sent: {message.ToJson()}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] Failed to send message: {e.Message}");
            Disconnect();
        }
    }

    /// <summary>
    /// Continuously receives messages from the server on a background thread.
    /// </summary>
    private void ReceiveMessages()
    {
        try
        {
            while (isConnected && client != null && client.Connected)
            {
                string json = reader.ReadLine();
                if (json == null)
                {
                    Debug.Log("[GameClient] Server closed connection.");
                    break;
                }

                NetworkMessage message = NetworkMessage.FromJson(json);
                Debug.Log($"[GameClient] Received: {json}");

                OnMessageReceived?.Invoke(message);
            }
        }
        catch (IOException)
        {
            Debug.Log("[GameClient] Disconnected from server.");
        }
        catch (Exception e)
        {
            if (isConnected)
                Debug.LogError($"[GameClient] Error receiving messages: {e.Message}");
        }
        finally
        {
            isConnected = false;
        }
    }

    /// <summary>
    /// Disconnects from the server and cleans up resources.
    /// </summary>
    public void Disconnect()
    {
        isConnected = false;

        try { reader?.Close(); }
        catch (Exception) { /* Ignore cleanup errors */ }

        try { stream?.Close(); }
        catch (Exception) { /* Ignore cleanup errors */ }

        try { client?.Close(); }
        catch (Exception) { /* Ignore cleanup errors */ }

        Debug.Log("[GameClient] Disconnected.");
    }
}
