using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP-based game server for a turn-based multiplayer game.
/// Manages game states and handles connections for exactly 2 players.
/// </summary>
public class GameServer : MonoBehaviour
{
    /// <summary>
    /// Possible states of the game managed by the server.
    /// </summary>
    public enum GameState
    {
        WAITING,
        PLACING,
        BATTLE,
        GAMEOVER
    }

    [Header("Server Settings")]
    [SerializeField] private int port = 7777;

    private TcpListener server;
    private readonly List<TcpClient> connectedClients = new List<TcpClient>();
    private readonly Dictionary<TcpClient, int> clientPlayerIds = new Dictionary<TcpClient, int>();
    private GameState currentState = GameState.WAITING;
    private int currentPlayerTurn;
    private Thread listenThread;
    private volatile bool isRunning;

    private const int MaxPlayers = 2;

    /// <summary>The current state of the game.</summary>
    public GameState CurrentState => currentState;

    /// <summary>The number of currently connected players.</summary>
    public int ConnectedPlayerCount => connectedClients.Count;

    private void Start()
    {
        StartServer();
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }

    /// <summary>
    /// Starts the TCP server and begins listening for client connections.
    /// </summary>
    public void StartServer()
    {
        try
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            isRunning = true;
            currentState = GameState.WAITING;

            listenThread = new Thread(ListenForClients)
            {
                IsBackground = true
            };
            listenThread.Start();

            Debug.Log($"[GameServer] Server started on port {port}. Waiting for players...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameServer] Failed to start server: {e.Message}");
        }
    }

    /// <summary>
    /// Stops the server and disconnects all clients.
    /// </summary>
    public void StopServer()
    {
        isRunning = false;

        lock (connectedClients)
        {
            foreach (TcpClient client in connectedClients)
            {
                try { client.Close(); }
                catch (Exception) { /* Ignore cleanup errors */ }
            }
            connectedClients.Clear();
            clientPlayerIds.Clear();
        }

        try { server?.Stop(); }
        catch (Exception) { /* Ignore cleanup errors */ }

        Debug.Log("[GameServer] Server stopped.");
    }

    /// <summary>
    /// Listens for incoming client connections on a background thread.
    /// Accepts up to <see cref="MaxPlayers"/> clients.
    /// </summary>
    private void ListenForClients()
    {
        try
        {
            while (isRunning)
            {
                if (!server.Pending())
                {
                    Thread.Sleep(100);
                    continue;
                }

                TcpClient client = server.AcceptTcpClient();

                lock (connectedClients)
                {
                    if (connectedClients.Count >= MaxPlayers)
                    {
                        Debug.Log("[GameServer] Connection rejected: server is full.");
                        client.Close();
                        continue;
                    }

                    int playerId = connectedClients.Count + 1;
                    connectedClients.Add(client);
                    clientPlayerIds[client] = playerId;

                    Debug.Log($"[GameServer] Player {playerId} connected. ({connectedClients.Count}/{MaxPlayers})");

                    Thread clientThread = new Thread(() => HandleClient(client))
                    {
                        IsBackground = true
                    };
                    clientThread.Start();

                    if (connectedClients.Count == MaxPlayers)
                    {
                        OnAllPlayersConnected();
                    }
                }
            }
        }
        catch (SocketException e)
        {
            if (isRunning)
                Debug.LogError($"[GameServer] Socket error while listening: {e.Message}");
        }
    }

    /// <summary>
    /// Called when both players have connected. Transitions to the PLACING state.
    /// </summary>
    private void OnAllPlayersConnected()
    {
        currentState = GameState.PLACING;
        Debug.Log("[GameServer] All players connected. State -> PLACING");

        // Notify both players that the game is starting
        NetworkMessage startMsg = new NetworkMessage
        {
            eventType = NetworkMessage.EventType.TURN_CHANGE,
            payload = "PLACING"
        };
        BroadcastMessage(startMsg);
    }

    /// <summary>
    /// Handles communication with a single connected client on a dedicated thread.
    /// </summary>
    /// <param name="client">The connected TcpClient.</param>
    private void HandleClient(TcpClient client)
    {
        int playerId = clientPlayerIds[client];
        NetworkStream stream = client.GetStream();
        StreamReader reader = new StreamReader(stream, Encoding.UTF8);

        try
        {
            while (isRunning && client.Connected)
            {
                string json = reader.ReadLine();
                if (json == null)
                    break;

                NetworkMessage message = NetworkMessage.FromJson(json);
                message.playerId = playerId;

                Debug.Log($"[GameServer] Received from Player {playerId}: {json}");
                ProcessMessage(message, client);
            }
        }
        catch (IOException)
        {
            Debug.Log($"[GameServer] Player {playerId} disconnected.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameServer] Error handling Player {playerId}: {e.Message}");
        }
        finally
        {
            RemoveClient(client);
        }
    }

    /// <summary>
    /// Processes an incoming message from a client based on the current game state.
    /// </summary>
    /// <param name="message">The received NetworkMessage.</param>
    /// <param name="sender">The TcpClient that sent the message.</param>
    private void ProcessMessage(NetworkMessage message, TcpClient sender)
    {
        switch (message.eventType)
        {
            case NetworkMessage.EventType.SHOOT:
                HandleShoot(message, sender);
                break;

            default:
                Debug.Log($"[GameServer] Unhandled event type: {message.eventType}");
                break;
        }
    }

    /// <summary>
    /// Handles a SHOOT action from a player during the BATTLE state.
    /// Validates turn order, forwards the shot to the opponent, and switches turns.
    /// </summary>
    /// <param name="message">The shoot message containing coordinates.</param>
    /// <param name="sender">The client that fired the shot.</param>
    private void HandleShoot(NetworkMessage message, TcpClient sender)
    {
        if (currentState != GameState.BATTLE)
        {
            Debug.Log("[GameServer] SHOOT ignored: not in BATTLE state.");
            return;
        }

        int senderId = clientPlayerIds[sender];
        if (senderId != currentPlayerTurn)
        {
            Debug.Log($"[GameServer] SHOOT ignored: not Player {senderId}'s turn.");
            return;
        }

        // Forward the shot to the opponent
        TcpClient opponent = GetOpponent(sender);
        if (opponent != null)
        {
            SendMessageToClient(opponent, message);
        }

        // Switch turns
        SwitchTurn();
    }

    /// <summary>
    /// Switches the current turn to the other player and notifies both clients.
    /// </summary>
    private void SwitchTurn()
    {
        currentPlayerTurn = currentPlayerTurn == 1 ? 2 : 1;

        NetworkMessage turnMsg = new NetworkMessage
        {
            eventType = NetworkMessage.EventType.TURN_CHANGE,
            playerId = currentPlayerTurn
        };
        BroadcastMessage(turnMsg);

        // Notify the active player that it's their turn
        TcpClient activeClient = GetClientByPlayerId(currentPlayerTurn);
        if (activeClient != null)
        {
            NetworkMessage yourTurnMsg = new NetworkMessage
            {
                eventType = NetworkMessage.EventType.YOUR_TURN,
                playerId = currentPlayerTurn
            };
            SendMessageToClient(activeClient, yourTurnMsg);
        }
    }

    /// <summary>
    /// Transitions the game to the BATTLE state and assigns the first turn.
    /// </summary>
    public void StartBattle()
    {
        currentState = GameState.BATTLE;
        currentPlayerTurn = 1;

        Debug.Log("[GameServer] State -> BATTLE. Player 1 starts.");

        NetworkMessage turnMsg = new NetworkMessage
        {
            eventType = NetworkMessage.EventType.TURN_CHANGE,
            playerId = currentPlayerTurn,
            payload = "BATTLE"
        };
        BroadcastMessage(turnMsg);

        TcpClient firstPlayer = GetClientByPlayerId(1);
        if (firstPlayer != null)
        {
            NetworkMessage yourTurnMsg = new NetworkMessage
            {
                eventType = NetworkMessage.EventType.YOUR_TURN,
                playerId = 1
            };
            SendMessageToClient(firstPlayer, yourTurnMsg);
        }
    }

    /// <summary>
    /// Ends the game and notifies all players of the winner.
    /// </summary>
    /// <param name="winnerId">The player ID of the winner.</param>
    public void EndGame(int winnerId)
    {
        currentState = GameState.GAMEOVER;

        NetworkMessage gameOverMsg = new NetworkMessage
        {
            eventType = NetworkMessage.EventType.GAME_OVER,
            playerId = winnerId,
            payload = $"Player {winnerId} wins!"
        };
        BroadcastMessage(gameOverMsg);

        Debug.Log($"[GameServer] State -> GAMEOVER. Player {winnerId} wins!");
    }

    /// <summary>
    /// Sends a NetworkMessage to a specific client.
    /// </summary>
    /// <param name="client">The target TcpClient.</param>
    /// <param name="message">The message to send.</param>
    private void SendMessageToClient(TcpClient client, NetworkMessage message)
    {
        try
        {
            if (client == null || !client.Connected)
                return;

            string json = message.ToJson() + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            NetworkStream stream = client.GetStream();
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameServer] Failed to send message: {e.Message}");
        }
    }

    /// <summary>
    /// Broadcasts a NetworkMessage to all connected clients.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    private void BroadcastMessage(NetworkMessage message)
    {
        lock (connectedClients)
        {
            foreach (TcpClient client in connectedClients)
            {
                SendMessageToClient(client, message);
            }
        }
    }

    /// <summary>
    /// Returns the opponent TcpClient of the given client.
    /// </summary>
    /// <param name="client">The client whose opponent to find.</param>
    /// <returns>The opponent's TcpClient, or null if not found.</returns>
    private TcpClient GetOpponent(TcpClient client)
    {
        lock (connectedClients)
        {
            foreach (TcpClient other in connectedClients)
            {
                if (other != client)
                    return other;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns the TcpClient associated with a given player ID.
    /// </summary>
    /// <param name="playerId">The player ID to look up.</param>
    /// <returns>The associated TcpClient, or null if not found.</returns>
    private TcpClient GetClientByPlayerId(int playerId)
    {
        lock (connectedClients)
        {
            foreach (TcpClient client in connectedClients)
            {
                if (clientPlayerIds.TryGetValue(client, out int id) && id == playerId)
                    return client;
            }
        }
        return null;
    }

    /// <summary>
    /// Removes a disconnected client from the server's tracking collections.
    /// </summary>
    /// <param name="client">The client to remove.</param>
    private void RemoveClient(TcpClient client)
    {
        lock (connectedClients)
        {
            if (clientPlayerIds.TryGetValue(client, out int playerId))
            {
                Debug.Log($"[GameServer] Removing Player {playerId}.");
                clientPlayerIds.Remove(client);
            }
            connectedClients.Remove(client);

            try { client.Close(); }
            catch (Exception) { /* Ignore cleanup errors */ }
        }
    }
}
