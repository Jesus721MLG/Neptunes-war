using System;
using System.Collections.Generic;

/// <summary>
/// Result of a shot fired at the board.
/// </summary>
public enum ShotResult
{
    HIT,
    MISS,
    SUNK
}

/// <summary>
/// Manages a 10×10 logical grid for ship placement and shooting.
/// </summary>
public class Tablero
{
    /// <summary>Board width and height.</summary>
    public const int BoardSize = 10;

    // Each cell stores the ship occupying it, or null if empty.
    private readonly Barco[,] grid = new Barco[BoardSize, BoardSize];

    // Tracks which cells have already been targeted.
    private readonly bool[,] shots = new bool[BoardSize, BoardSize];

    // All ships that have been placed on this board.
    private readonly List<Barco> ships = new List<Barco>();

    /// <summary>A read-only list of ships placed on this board.</summary>
    public IReadOnlyList<Barco> Ships => ships;

    /// <summary>
    /// Determines whether a ship can be placed at the given position
    /// without going out of bounds or overlapping another ship.
    /// </summary>
    /// <param name="x">The starting column (0-based).</param>
    /// <param name="y">The starting row (0-based).</param>
    /// <param name="ship">The ship to place.</param>
    /// <returns>True if the ship fits and does not overlap; otherwise false.</returns>
    public bool CanPlaceShip(int x, int y, Barco ship)
    {
        if (ship == null)
            return false;

        for (int i = 0; i < ship.Size; i++)
        {
            int cellX = x + (ship.Orientation == Orientation.Horizontal ? i : 0);
            int cellY = y + (ship.Orientation == Orientation.Vertical ? i : 0);

            // Check bounds
            if (cellX < 0 || cellX >= BoardSize || cellY < 0 || cellY >= BoardSize)
                return false;

            // Check overlap
            if (grid[cellX, cellY] != null)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Places a ship on the board at the specified position.
    /// The position must have been validated with <see cref="CanPlaceShip"/> first.
    /// </summary>
    /// <param name="x">The starting column (0-based).</param>
    /// <param name="y">The starting row (0-based).</param>
    /// <param name="ship">The ship to place.</param>
    /// <returns>True if the ship was placed successfully; false if placement is invalid.</returns>
    public bool PlaceShip(int x, int y, Barco ship)
    {
        if (!CanPlaceShip(x, y, ship))
            return false;

        for (int i = 0; i < ship.Size; i++)
        {
            int cellX = x + (ship.Orientation == Orientation.Horizontal ? i : 0);
            int cellY = y + (ship.Orientation == Orientation.Vertical ? i : 0);
            grid[cellX, cellY] = ship;
        }

        ships.Add(ship);
        return true;
    }

    /// <summary>
    /// Fires a shot at the specified cell.
    /// </summary>
    /// <param name="x">The target column (0-based).</param>
    /// <param name="y">The target row (0-based).</param>
    /// <returns>The result of the shot: HIT, MISS, or SUNK.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If coordinates are outside the board.</exception>
    /// <exception cref="InvalidOperationException">If the cell has already been targeted.</exception>
    public ShotResult Shoot(int x, int y)
    {
        if (x < 0 || x >= BoardSize || y < 0 || y >= BoardSize)
            throw new ArgumentOutOfRangeException($"Coordinates ({x}, {y}) are outside the board.");

        if (shots[x, y])
            throw new InvalidOperationException($"Cell ({x}, {y}) has already been targeted.");

        shots[x, y] = true;

        Barco ship = grid[x, y];
        if (ship == null)
            return ShotResult.MISS;

        ship.Hit();
        return ship.IsSunk ? ShotResult.SUNK : ShotResult.HIT;
    }

    /// <summary>
    /// Returns whether a cell has already been shot at.
    /// </summary>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <returns>True if the cell was already targeted.</returns>
    public bool HasBeenShot(int x, int y)
    {
        if (x < 0 || x >= BoardSize || y < 0 || y >= BoardSize)
            return false;

        return shots[x, y];
    }

    /// <summary>
    /// Returns whether all ships on this board have been sunk.
    /// Returns false if no ships have been placed.
    /// </summary>
    /// <returns>True if at least one ship exists and every ship is sunk; otherwise false.</returns>
    public bool AllShipsSunk()
    {
        foreach (Barco ship in ships)
        {
            if (!ship.IsSunk)
                return false;
        }
        return ships.Count > 0;
    }
}
