# Maze Solver with LLM - Design Document

## Overview

A C# WPF application that uses an LLM (Claude Sonnet 4.5) to navigate through a maze. The LLM is given a single tool `GetNeighbours` to explore the maze and find the path from entry to exit.

This project serves as a test case for exploring LLM context overflow behavior.

## Goals

1. **Maze Generation**: Generate solvable mazes with configurable dimensions (default 100x100)
2. **GUI**: Visual representation of the maze with ability to toggle walls/paths
3. **LLM Solving**: Use Claude via tool calling to navigate the maze
4. **Context Tracking**: Real-time display of token usage and context size
5. **Context Overflow Detection**: Catch and display context overflow errors
6. **CLI Support**: Command-line interface for automated testing
7. **Logging**: Comprehensive file logging for debugging

## Technical Stack

- **Framework**: .NET 8.0
- **GUI**: WPF (Windows Presentation Foundation)
- **LLM SDK**: Anthropic C# SDK (`Anthropic` NuGet package)
- **Model**: Claude Sonnet 4.5 (`claude-sonnet-4-5-2`)
- **Logging**: Serilog (file and console sinks)

## Architecture

```
MazeSolver/
├── MazeSolver.sln
├── src/
│   └── MazeSolver/
│       ├── MazeSolver.csproj
│       ├── Program.cs              # Entry point, CLI handling
│       ├── App.xaml                # WPF App definition
│       ├── App.xaml.cs
│       ├── MainWindow.xaml         # Main GUI window
│       ├── MainWindow.xaml.cs
│       ├── Models/
│       │   ├── Cell.cs             # Cell state (Path, Wall, Entry, Exit)
│       │   ├── Maze.cs             # Maze grid and generation
│       │   └── Position.cs         # (X, Y) coordinate
│       ├── Services/
│       │   ├── MazeGenerator.cs    # Random maze generation with DFS
│       │   ├── LlmService.cs       # Anthropic API wrapper
│       │   └── MazeSolverService.cs # Tool calling loop
│       └── ViewModels/
│           └── MainViewModel.cs    # MVVM ViewModel
└── DESIGN.md
```

## Model Details

### Claude Sonnet 4.5 Specifications
- **Context Window**: 200,000 tokens
- **Max Output**: 8,192 tokens (default), up to 64K with extended thinking
- **Model ID**: `claude-sonnet-4-5-2` (Azure) / `claude-sonnet-4-5-20250929`

## Configuration

Environment variables (configured in launchSettings.json):
```json
{
  "environmentVariables": {
    "LLM_ENDPOINT": "<your-azure-ai-endpoint>",
    "LLM_MODEL": "<your-model-name>",
    "LLM_API_KEY": "<your-api-key>"
  }
}
```

## Tool Definition

### GetNeighbours Tool

```json
{
  "name": "GetNeighbours",
  "description": "Get the status of all 8 neighbouring cells around a given position. Returns status for each direction: N, NE, E, SE, S, SW, W, NW. Status can be 'path', 'wall', 'exit', or 'out_of_bounds'.",
  "input_schema": {
    "type": "object",
    "properties": {
      "x": {
        "type": "integer",
        "description": "X coordinate (column) of the cell to check neighbours for"
      },
      "y": {
        "type": "integer",
        "description": "Y coordinate (row) of the cell to check neighbours for"
      }
    },
    "required": ["x", "y"]
  }
}
```

### Tool Response Format

```json
{
  "position": {"x": 5, "y": 10},
  "neighbours": {
    "N":  {"x": 5, "y": 9,  "status": "path"},
    "NE": {"x": 6, "y": 9,  "status": "wall"},
    "E":  {"x": 6, "y": 10, "status": "path"},
    "SE": {"x": 6, "y": 11, "status": "wall"},
    "S":  {"x": 5, "y": 11, "status": "path"},
    "SW": {"x": 4, "y": 11, "status": "wall"},
    "W":  {"x": 4, "y": 10, "status": "wall"},
    "NW": {"x": 4, "y": 9,  "status": "out_of_bounds"}
  }
}
```

## System Prompt

```
You are a maze-solving AI. Your task is to find a path from the entry point to the exit.

You have one tool available: GetNeighbours(x, y)
This tool returns the status of all 8 cells around the given position.

Status values:
- "path": A walkable cell you can move to
- "wall": An impassable cell
- "exit": The goal - the maze exit!
- "out_of_bounds": Outside the maze boundaries

Strategy:
1. Start from the entry position provided
2. Use GetNeighbours to explore reachable cells
3. Keep track of visited cells to avoid loops
4. When you find a neighbour with status "exit", you've solved the maze!
5. Report the path you found

Current entry position: ({entry_x}, {entry_y})
Maze dimensions: {width} x {height}

Call GetNeighbours repeatedly to explore the maze. When you find the exit, respond with the complete path.
```

## Token Usage Tracking

The Anthropic API returns usage information in each response:

```csharp
// From Message response
message.Usage.InputTokens   // Tokens in the request
message.Usage.OutputTokens  // Tokens in the response
```

### Context Tracking Display

The GUI will show:
- **Max Context**: 200,000 tokens (fixed for Claude Sonnet 4.5)
- **Current Usage**: Sum of all input + output tokens in conversation
- **Remaining**: Max - Current
- **Visual Progress Bar**: Showing context fill level

## Context Overflow Handling

The API will return an error when context is exceeded. We'll catch this:

```csharp
try
{
    var response = await client.Messages.Create(parameters);
}
catch (AnthropicBadRequestException ex) when (ex.Message.Contains("context") || ex.Message.Contains("token"))
{
    // Context overflow detected
    OnContextOverflow(ex.Message);
}
```

## Iterative Development Plan

### Iteration 1: LLM Connection Test ✅
**Goal**: Verify connection to Claude via Azure endpoint

Files to create:
- MazeSolver.csproj (with Anthropic NuGet)
- Program.cs (simple test)
- launchSettings.json

Test: Send "Hello" message, receive response

### Iteration 2: CLI Maze Game
**Goal**: Working maze generation and LLM solving via CLI

Files to create/modify:
- Models/Cell.cs, Position.cs, Maze.cs
- Services/MazeGenerator.cs
- Services/LlmService.cs
- Services/MazeSolverService.cs
- Update Program.cs for CLI

Test: Generate 10x10 maze, solve via CLI, log path

### Iteration 3: WPF GUI
**Goal**: Visual maze display with interactive features

Files to create:
- App.xaml, App.xaml.cs
- MainWindow.xaml, MainWindow.xaml.cs
- ViewModels/MainViewModel.cs

Features:
- Maze grid display
- Cell click to toggle wall/path
- Dimension input (width, height)
- Generate button
- Solve button
- Tool call counter display
- Visited cells highlighting (green)

### Iteration 4: Context Tracking & Overflow
**Goal**: Real-time token usage display and overflow handling

Additions:
- Token usage panel in GUI
- Progress bar for context fill
- Real-time updates during solving
- Overflow exception handling
- Overflow visual indicator

### Iteration 5: Polish & Testing
**Goal**: Full integration and edge case handling

- End-to-end testing
- Large maze testing (100x100)
- Context overflow testing
- Logging verification
- CLI automation testing

## GUI Layout

```
+------------------------------------------------------------------+
|  Maze Solver with LLM                                    [_][□][X]|
+------------------------------------------------------------------+
| Dimensions: Width [100] Height [100]  [Generate]  [Solve]        |
+------------------------------------------------------------------+
|                                                                   |
|  +----------------------------------------------------------+    |
|  |                                                          |    |
|  |                    MAZE GRID                             |    |
|  |                                                          |    |
|  |  [Entry = Green Start, Exit = Red, Path = White,         |    |
|  |   Wall = Black, Visited = Light Green]                   |    |
|  |                                                          |    |
|  +----------------------------------------------------------+    |
|                                                                   |
+------------------------------------------------------------------+
| Status: Idle / Solving... / Solved! / Context Overflow!          |
+------------------------------------------------------------------+
| Tool Calls: 0          | Context: 0 / 200,000 tokens             |
| [=================>                    ] 45%                      |
+------------------------------------------------------------------+
```

## Logging Strategy

Using Serilog with:
- Console sink (for CLI visibility)
- File sink (logs/maze-solver-{date}.log)

Log levels:
- **Debug**: Every tool call, cell status
- **Information**: Maze generation, solve start/end, path found
- **Warning**: Approaching context limit (>80%)
- **Error**: Context overflow, API errors

## CLI Arguments

```
MazeSolver.exe [options]

Options:
  --cli             Run in CLI mode (no GUI)
  --width <n>       Maze width (default: 100)
  --height <n>      Maze height (default: 100)
  --auto-solve      Automatically start solving
  --log-file <path> Custom log file path
  --help            Show help
```

## Dependencies (NuGet Packages)

```xml
<PackageReference Include="Anthropic" Version="10.*" />
<PackageReference Include="Serilog" Version="4.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
<PackageReference Include="Serilog.Sinks.File" Version="6.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
```

## Key Implementation Notes

1. **One-Shot Tool Loop**: The LLM solving is a single conversation with multiple tool calls, not separate calls.

2. **Message History**: Each tool result is appended to messages array, building up context.

3. **Token Accumulation**: Track cumulative tokens across all turns in the conversation.

4. **Azure Endpoint**: The Anthropic SDK can connect to Azure-hosted Claude via custom BaseUrl.

5. **Stop Reason**: Check `message.StopReason` - "tool_use" means continue loop, "end_turn" means done.

## Error Handling

| Error Type | Handling |
|------------|----------|
| Connection failure | Retry 3x, then show error in GUI |
| Invalid API key | Show authentication error |
| Context overflow | Catch exception, display in GUI, stop solving |
| Invalid tool call | Return error message to LLM, let it retry |
| Maze unsolvable | Should not happen (generator ensures solvability) |

---

## Current Status

- [x] Design document created
- [ ] Iteration 1: LLM Connection
- [ ] Iteration 2: CLI Maze Game  
- [ ] Iteration 3: WPF GUI
- [ ] Iteration 4: Context Tracking
- [ ] Iteration 5: Testing
