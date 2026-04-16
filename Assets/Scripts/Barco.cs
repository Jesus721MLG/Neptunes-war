using System;

/// <summary>
/// Types of ships available in the game, each with a fixed size.
/// </summary>
public enum ShipType
{
    Portaaviones, // Size 4
    Acorazado,    // Size 3
    Destructor,   // Size 3
    Submarino,    // Size 2
    Patrullero    // Size 2
}

/// <summary>
/// Orientation of a ship on the board.
/// </summary>
public enum Orientation
{
    Horizontal,
    Vertical
}

/// <summary>
/// Represents a ship (Barco) with a type, orientation, and current health.
/// </summary>
[Serializable]
public class Barco
{
    /// <summary>The type of this ship.</summary>
    public ShipType Type { get; private set; }

    /// <summary>The orientation of this ship on the board.</summary>
    public Orientation Orientation { get; set; }

    /// <summary>The size (number of cells) of this ship, determined by its type.</summary>
    public int Size { get; private set; }

    /// <summary>The current health of this ship. Starts equal to Size and decreases on hits.</summary>
    public int Health { get; private set; }

    /// <summary>Whether this ship has been sunk (health reached zero).</summary>
    public bool IsSunk => Health <= 0;

    /// <summary>
    /// Creates a new ship of the given type and orientation.
    /// </summary>
    /// <param name="type">The ship type.</param>
    /// <param name="orientation">The ship orientation.</param>
    public Barco(ShipType type, Orientation orientation)
    {
        Type = type;
        Orientation = orientation;
        Size = GetSizeForType(type);
        Health = Size;
    }

    /// <summary>
    /// Applies one hit to this ship, reducing its health by one.
    /// </summary>
    public void Hit()
    {
        if (Health > 0)
            Health--;
    }

    /// <summary>
    /// Returns the size for a given ship type.
    /// </summary>
    /// <param name="type">The ship type.</param>
    /// <returns>The number of cells the ship occupies.</returns>
    public static int GetSizeForType(ShipType type)
    {
        switch (type)
        {
            case ShipType.Portaaviones: return 4;
            case ShipType.Acorazado:    return 3;
            case ShipType.Destructor:   return 3;
            case ShipType.Submarino:    return 2;
            case ShipType.Patrullero:   return 2;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown ship type.");
        }
    }
}
