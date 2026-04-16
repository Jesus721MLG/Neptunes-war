using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game manager that links the <see cref="GameClient"/>,
/// <see cref="Tablero"/>, and <see cref="UIManager"/>.
/// Handles the placement timer and enforces turn-based shooting.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameClient gameClient;
    [SerializeField] private UIManager uiManager;

    [Header("Placement Settings")]
    [Tooltip("Duration of the ship-placement phase in seconds.")]
    [SerializeField] private float placementTimeLimit = 90f;

    /// <summary>The local player's board.</summary>
    private Tablero localBoard;

    /// <summary>Whether it is this player's turn to shoot.</summary>
    private bool isMyTurn;

    /// <summary>Placement timer coroutine handle.</summary>
    private Coroutine placementTimerCoroutine;

    /// <summary>Messages received on background threads, dispatched on the main thread.</summary>
    private readonly Queue<NetworkMessage> incomingMessages = new Queue<NetworkMessage>();

    /// <summary>Assigned player ID received from the server.</summary>
    private int localPlayerId;

    private void Awake()
    {
        localBoard = new Tablero();
    }

    private void Start()
    {
        if (uiManager != null)
            uiManager.ShowMainMenu();
    }

    private void OnEnable()
    {
        if (gameClient != null)
            gameClient.OnMessageReceived += EnqueueMessage;
    }

    private void OnDisable()
    {
        if (gameClient != null)
            gameClient.OnMessageReceived -= EnqueueMessage;
    }

    private void Update()
    {
        ProcessIncomingMessages();
    }

    // ------------------------------------------------------------------
    // Public actions (called by UI buttons)
    // ------------------------------------------------------------------

    /// <summary>
    /// Called when the player presses "Play" on the main menu.
    /// Connects to the server and switches to the waiting room.
    /// </summary>
    public void OnPlayButtonPressed()
    {
        if (gameClient != null)
            gameClient.ConnectToServer();

        if (uiManager != null)
            uiManager.ShowWaitingRoom();
    }

    /// <summary>
    /// Called when the player presses "Ready" after placing all ships.
    /// </summary>
    public void OnReadyButtonPressed()
    {
        if (placementTimerCoroutine != null)
            StopCoroutine(placementTimerCoroutine);

        NetworkMessage readyMsg = new NetworkMessage
        {
            eventType = NetworkMessage.EventType.TURN_CHANGE,
            payload = "READY"
        };
        SendNetworkMessage(readyMsg);
    }

    /// <summary>
    /// Attempts to send a SHOOT message to the server.
    /// The shot is only sent when it is this player's turn.
    /// </summary>
    /// <param name="x">Target column (0-based).</param>
    /// <param name="y">Target row (0-based).</param>
    public void RequestShoot(int x, int y)
    {
        if (!isMyTurn)
        {
            Debug.LogWarning("[GameManager] Cannot shoot: it is not your turn.");
            return;
        }

        NetworkMessage shootMsg = new NetworkMessage
        {
            eventType = NetworkMessage.EventType.SHOOT,
            x = x,
            y = y
        };
        SendNetworkMessage(shootMsg);

        // Disable further shooting until the server grants the next turn.
        isMyTurn = false;

        if (uiManager != null)
        {
            uiManager.SetAttackGridInteractable(false);
            uiManager.UpdateTurnLabel("Opponent's Turn");
        }
    }

    // ------------------------------------------------------------------
    // Networking helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Thread-safe enqueue of a message received from <see cref="GameClient"/>.
    /// </summary>
    private void EnqueueMessage(NetworkMessage message)
    {
        lock (incomingMessages)
        {
            incomingMessages.Enqueue(message);
        }
    }

    /// <summary>
    /// Drains the message queue on the main thread and dispatches each message.
    /// </summary>
    private void ProcessIncomingMessages()
    {
        lock (incomingMessages)
        {
            while (incomingMessages.Count > 0)
            {
                NetworkMessage msg = incomingMessages.Dequeue();
                HandleMessage(msg);
            }
        }
    }

    /// <summary>
    /// Sends a <see cref="NetworkMessage"/> through the <see cref="GameClient"/>.
    /// </summary>
    private void SendNetworkMessage(NetworkMessage message)
    {
        if (gameClient != null)
            gameClient.SendMessage(message);
    }

    // ------------------------------------------------------------------
    // Message handling
    // ------------------------------------------------------------------

    /// <summary>
    /// Dispatches a received message to the appropriate handler.
    /// </summary>
    private void HandleMessage(NetworkMessage message)
    {
        if (message == null) return;

        switch (message.eventType)
        {
            case NetworkMessage.EventType.TURN_CHANGE:
                HandleTurnChange(message);
                break;

            case NetworkMessage.EventType.YOUR_TURN:
                HandleYourTurn(message);
                break;

            case NetworkMessage.EventType.SHOOT:
                HandleIncomingShot(message);
                break;

            case NetworkMessage.EventType.GAME_OVER:
                HandleGameOver(message);
                break;

            default:
                Debug.Log($"[GameManager] Unhandled event: {message.eventType}");
                break;
        }
    }

    /// <summary>
    /// Handles a TURN_CHANGE message from the server, which signals
    /// phase transitions (PLACING / BATTLE) or general turn updates.
    /// </summary>
    private void HandleTurnChange(NetworkMessage message)
    {
        if (message.payload == "PLACING")
        {
            if (uiManager != null)
                uiManager.ShowPlacing();

            placementTimerCoroutine = StartCoroutine(PlacementTimerCoroutine());
        }
        else if (message.payload == "BATTLE")
        {
            if (uiManager != null)
                uiManager.ShowBattle();
        }

        // Store the player ID the server assigned if provided.
        if (message.playerId > 0 && localPlayerId == 0)
            localPlayerId = message.playerId;
    }

    /// <summary>
    /// Handles a YOUR_TURN message, enabling the player to shoot.
    /// </summary>
    private void HandleYourTurn(NetworkMessage message)
    {
        isMyTurn = true;

        if (uiManager != null)
        {
            uiManager.SetAttackGridInteractable(true);
            uiManager.UpdateTurnLabel("Your Turn");
        }

        Debug.Log("[GameManager] It is your turn.");
    }

    /// <summary>
    /// Handles an incoming SHOOT message (the opponent's shot at the local board).
    /// Resolves the shot on <see cref="localBoard"/> and updates the defense grid.
    /// </summary>
    private void HandleIncomingShot(NetworkMessage message)
    {
        int x = message.x;
        int y = message.y;

        ShotResult result;
        try
        {
            result = localBoard.Shoot(x, y);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameManager] Shot error at ({x},{y}): {e.Message}");
            return;
        }

        if (uiManager != null)
            uiManager.UpdateDefenseCell(x, y, result);

        Debug.Log($"[GameManager] Opponent shot ({x},{y}) => {result}");

        // Check for game over.
        if (localBoard.AllShipsSunk())
        {
            NetworkMessage gameOverMsg = new NetworkMessage
            {
                eventType = NetworkMessage.EventType.GAME_OVER,
                payload = "DEFEAT"
            };
            SendNetworkMessage(gameOverMsg);
        }
    }

    /// <summary>
    /// Handles the GAME_OVER message and shows the result screen.
    /// </summary>
    private void HandleGameOver(NetworkMessage message)
    {
        isMyTurn = false;

        if (uiManager != null)
        {
            uiManager.SetAttackGridInteractable(false);
            uiManager.ShowGameOver(message.payload ?? "Game Over");
        }

        Debug.Log($"[GameManager] Game over — {message.payload}");
    }

    // ------------------------------------------------------------------
    // Placement timer
    // ------------------------------------------------------------------

    /// <summary>
    /// Coroutine that counts down the placement phase.
    /// When time expires the player is automatically readied.
    /// </summary>
    private IEnumerator PlacementTimerCoroutine()
    {
        float remaining = placementTimeLimit;

        while (remaining > 0f)
        {
            if (uiManager != null)
                uiManager.UpdateTimerDisplay(Mathf.CeilToInt(remaining));

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        if (uiManager != null)
            uiManager.UpdateTimerDisplay(0);

        Debug.Log("[GameManager] Placement time expired. Auto-readying.");
        OnReadyButtonPressed();
    }
}
