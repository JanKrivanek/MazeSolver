namespace MazeSolver.Models;

/// <summary>
/// Represents a position in the maze grid
/// </summary>
public readonly record struct Position(int X, int Y)
{
    public Position North => new(X, Y - 1);
    public Position NorthEast => new(X + 1, Y - 1);
    public Position East => new(X + 1, Y);
    public Position SouthEast => new(X + 1, Y + 1);
    public Position South => new(X, Y + 1);
    public Position SouthWest => new(X - 1, Y + 1);
    public Position West => new(X - 1, Y);
    public Position NorthWest => new(X - 1, Y - 1);

    public IEnumerable<(string Direction, Position Pos)> GetAllNeighbours()
    {
        yield return ("N", North);
        yield return ("NE", NorthEast);
        yield return ("E", East);
        yield return ("SE", SouthEast);
        yield return ("S", South);
        yield return ("SW", SouthWest);
        yield return ("W", West);
        yield return ("NW", NorthWest);
    }

    public override string ToString() => $"({X}, {Y})";
}
