using System;
using UnityEngine;

/// <summary>
/// Serializable network message used for JSON-based communication
/// between the game server and clients.
/// </summary>
[Serializable]
public class NetworkMessage
{
    /// <summary>
    /// Standard event types exchanged between server and clients.
    /// </summary>
    public static class EventType
    {
        public const string YOUR_TURN = "YOUR_TURN";
        public const string SHOOT = "SHOOT";
        public const string TURN_CHANGE = "TURN_CHANGE";
        public const string GAME_OVER = "GAME_OVER";
    }

    /// <summary>The event type of this message (e.g. YOUR_TURN, SHOOT).</summary>
    public string eventType;

    /// <summary>The player ID that sent or is targeted by this message.</summary>
    public int playerId;

    /// <summary>Optional X coordinate for actions like shooting.</summary>
    public int x;

    /// <summary>Optional Y coordinate for actions like shooting.</summary>
    public int y;

    /// <summary>Optional payload for additional data.</summary>
    public string payload;

    /// <summary>
    /// Serializes this message to a JSON string.
    /// </summary>
    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    /// <summary>
    /// Deserializes a JSON string into a NetworkMessage.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A NetworkMessage populated from the JSON data.</returns>
    public static NetworkMessage FromJson(string json)
    {
        return JsonUtility.FromJson<NetworkMessage>(json);
    }
}
