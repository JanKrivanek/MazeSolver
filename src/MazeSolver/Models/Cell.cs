namespace MazeSolver.Models;

/// <summary>
/// Represents the type/state of a cell in the maze
/// </summary>
public enum CellType
{
    Wall,
    Path,
    Entry,
    Exit
}

/// <summary>
/// Represents a single cell in the maze
/// </summary>
public class Cell
{
    public Position Position { get; }
    public CellType Type { get; set; }
    public bool IsVisitedByLlm { get; set; }

    public Cell(Position position, CellType type = CellType.Wall)
    {
        Position = position;
        Type = type;
        IsVisitedByLlm = false;
    }

    public string GetStatusString() => Type switch
    {
        CellType.Wall => "wall",
        CellType.Path => "path",
        CellType.Entry => "path", // Entry is walkable
        CellType.Exit => "exit",
        _ => "unknown"
    };

    public bool IsWalkable => Type != CellType.Wall;
}
