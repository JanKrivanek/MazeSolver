using MazeSolver.Models;
using MazeSolver.Services;
using Serilog;

namespace MazeSolver;

public class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Configure Serilog
        ConfigureLogging();

        Log.Information("=== Maze Solver Starting ===");
        Log.Information("Arguments: {Args}", string.Join(" ", args));

        // Simple manual argument parsing
        bool cli = args.Contains("--cli");
        bool autoSolve = args.Contains("--auto-solve");
        bool testConnection = args.Contains("--test-connection");
        int width = GetIntArg(args, "--width", 100);
        int height = GetIntArg(args, "--height", 100);

        try
        {
            if (testConnection)
            {
                TestConnectionAsync().GetAwaiter().GetResult();
                return 0;
            }

            if (cli)
            {
                RunCliAsync(width, height, autoSolve).GetAwaiter().GetResult();
            }
            else
            {
                RunGui(width, height, autoSolve);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static int GetIntArg(string[] args, string name, int defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out int value))
            {
                return value;
            }
        }
        return defaultValue;
    }

    private static void ConfigureLogging()
    {
        var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "maze-solver-.log");
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static async Task TestConnectionAsync()
    {
        Log.Information("Testing LLM connection...");
        Console.WriteLine("Testing LLM connection...");

        try
        {
            var llmService = new LlmService();
            var success = await llmService.TestConnectionAsync();

            if (success)
            {
                Console.WriteLine("✓ Connection successful!");
                Log.Information("Connection test passed");
            }
            else
            {
                Console.WriteLine("✗ Connection failed!");
                Log.Error("Connection test failed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Connection error: {ex.Message}");
            Log.Error(ex, "Connection test error");
        }
    }

    private static async Task RunCliAsync(int width, int height, bool autoSolve)
    {
        Log.Information("Running in CLI mode. Width: {Width}, Height: {Height}, AutoSolve: {AutoSolve}",
            width, height, autoSolve);

        Console.WriteLine($"Maze Solver CLI Mode");
        Console.WriteLine($"====================");
        Console.WriteLine();

        // Generate maze
        var generator = new MazeGenerator();
        var maze = generator.Generate(width, height);

        Console.WriteLine($"Generated {maze.Width}x{maze.Height} maze");
        Console.WriteLine($"Entry: {maze.Entry}");
        Console.WriteLine($"Exit: {maze.Exit}");
        Console.WriteLine();

        // Show maze if small enough
        if (width <= 50 && height <= 50)
        {
            Console.WriteLine("Maze:");
            Console.WriteLine(maze.Render());
        }

        if (!autoSolve)
        {
            Console.Write("Press Enter to solve or 'q' to quit: ");
            var input = Console.ReadLine();
            if (input?.ToLower() == "q")
            {
                return;
            }
        }

        // Solve the maze
        Console.WriteLine("Starting maze solver...");
        Console.WriteLine();

        var llmService = new LlmService();
        var solverService = new MazeSolverService(llmService);

        // Set up event handlers for CLI output
        solverService.OnToolCall += (s, e) =>
        {
            if (e.ToolCallNumber % 10 == 0 || e.ToolCallNumber <= 5)
            {
                Console.WriteLine($"  Tool call #{e.ToolCallNumber}: GetNeighbours({e.Position})");
            }
        };

        solverService.OnTokenUsage += (s, e) =>
        {
            if (e.TotalTokens % 5000 < 100) // Print every ~5000 tokens
            {
                Console.WriteLine($"  Tokens: {e.TotalTokens:N0} / {e.MaxTokens:N0} ({e.UsagePercentage:F1}%)");
            }
        };

        solverService.OnStatusChanged += (s, status) =>
        {
            Log.Information("Status: {Status}", status);
        };

        solverService.OnContextOverflow += (s, ex) =>
        {
            Console.WriteLine();
            Console.WriteLine("!!! CONTEXT OVERFLOW !!!");
            Console.WriteLine($"Error: {ex.Message}");
            Log.Error("Context overflow detected: {Message}", ex.Message);
        };

        var cts = new CancellationTokenSource();
        
        // Allow cancellation with Ctrl+C
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nCancelling...");
        };

        var result = await solverService.SolveAsync(maze, cts.Token);

        Console.WriteLine();
        Console.WriteLine("=== RESULT ===");
        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Tool calls: {result.ToolCallCount}");
        Console.WriteLine($"Total tokens: {result.TotalTokens:N0}");
        Console.WriteLine($"Context overflow: {result.IsContextOverflow}");
        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine("Solution:");
            Console.WriteLine(result.Message);
        }
        else
        {
            Console.WriteLine($"Failed: {result.Message}");
        }

        // Show maze with visited cells
        if (width <= 50 && height <= 50)
        {
            Console.WriteLine();
            Console.WriteLine("Maze with visited cells (· = visited):");
            Console.WriteLine(maze.Render(showVisited: true));
        }

        Log.Information("CLI run completed. Success: {Success}, Tool calls: {ToolCalls}, Tokens: {Tokens}",
            result.Success, result.ToolCallCount, result.TotalTokens);
    }

    private static void RunGui(int width, int height, bool autoSolve)
    {
        Log.Information("Running in GUI mode");

        var app = new App();
        app.InitializeComponent();
        
        var mainWindow = new MainWindow(width, height, autoSolve);
        app.Run(mainWindow);
    }
}
