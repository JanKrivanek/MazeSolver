namespace MazeSolver.Models;

/// <summary>
/// Represents the maze grid with cells
/// </summary>
public class Maze
{
    public int Width { get; }
    public int Height { get; }
    public Cell[,] Cells { get; }
    public Position Entry { get; private set; }
    public Position Exit { get; private set; }

    public Maze(int width, int height)
    {
        Width = width;
        Height = height;
        Cells = new Cell[width, height];

        // Initialize all cells as walls
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cells[x, y] = new Cell(new Position(x, y), CellType.Wall);
            }
        }
    }

    public Cell this[int x, int y]
    {
        get => Cells[x, y];
        set => Cells[x, y] = value;
    }

    public Cell this[Position pos]
    {
        get => Cells[pos.X, pos.Y];
        set => Cells[pos.X, pos.Y] = value;
    }

    public bool IsInBounds(Position pos) =>
        pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

    public bool IsInBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;

    public void SetEntry(Position pos)
    {
        if (IsInBounds(Entry))
        {
            Cells[Entry.X, Entry.Y].Type = CellType.Path;
        }
        Entry = pos;
        Cells[pos.X, pos.Y].Type = CellType.Entry;
    }

    public void SetExit(Position pos)
    {
        if (IsInBounds(Exit))
        {
            Cells[Exit.X, Exit.Y].Type = CellType.Path;
        }
        Exit = pos;
        Cells[pos.X, pos.Y].Type = CellType.Exit;
    }

    public string GetCellStatus(Position pos)
    {
        if (!IsInBounds(pos))
            return "out_of_bounds";
        return Cells[pos.X, pos.Y].GetStatusString();
    }

    public void ToggleCell(Position pos)
    {
        if (!IsInBounds(pos)) return;
        var cell = Cells[pos.X, pos.Y];
        
        // Don't toggle entry or exit
        if (cell.Type == CellType.Entry || cell.Type == CellType.Exit)
            return;

        cell.Type = cell.Type == CellType.Wall ? CellType.Path : CellType.Wall;
    }

    public void ClearLlmVisited()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Cells[x, y].IsVisitedByLlm = false;
            }
        }
    }

    public void MarkLlmVisited(Position pos)
    {
        if (IsInBounds(pos))
        {
            Cells[pos.X, pos.Y].IsVisitedByLlm = true;
        }
    }

    /// <summary>
    /// Renders the maze to a string for console output
    /// </summary>
    public string Render(bool showVisited = false)
    {
        var sb = new System.Text.StringBuilder();
        
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var cell = Cells[x, y];
                char c = cell.Type switch
                {
                    CellType.Wall => '█',
                    CellType.Entry => 'S',
                    CellType.Exit => 'E',
                    CellType.Path when showVisited && cell.IsVisitedByLlm => '·',
                    CellType.Path => ' ',
                    _ => '?'
                };
                sb.Append(c);
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}
