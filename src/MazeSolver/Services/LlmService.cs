using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Serilog;
using System.Text.Json;

namespace MazeSolver.Services;

/// <summary>
/// Wrapper service for Anthropic LLM API
/// </summary>
public class LlmService
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    
    /// <summary>
    /// Maximum context window for Claude Sonnet 4.5 (200K tokens)
    /// </summary>
    public const int MaxContextTokens = 200_000;

    public LlmService()
    {
        var endpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT") 
            ?? throw new InvalidOperationException("LLM_ENDPOINT environment variable not set");
        var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY") 
            ?? throw new InvalidOperationException("LLM_API_KEY environment variable not set");
        _model = Environment.GetEnvironmentVariable("LLM_MODEL") 
            ?? throw new InvalidOperationException("LLM_MODEL environment variable not set");

        Log.Information("Initializing LLM service with endpoint: {Endpoint}, model: {Model}", endpoint, _model);

        // Set environment variables for the SDK
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", apiKey);
        Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", endpoint);
        
        _client = new AnthropicClient();
    }

    /// <summary>
    /// Sends a message to the LLM and returns the response (with retry for rate limits)
    /// </summary>
    public async Task<LlmResponse> SendMessageAsync(
        string systemPrompt,
        List<MessageParam> messages,
        List<ToolUnion>? tools = null,
        int maxTokens = 4096,
        CancellationToken cancellationToken = default)
    {
        Log.Debug("Sending message to LLM. Messages count: {Count}, Has tools: {HasTools}", 
            messages.Count, tools != null);

        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = maxTokens,
            System = systemPrompt,
            Messages = messages,
            Tools = tools
        };

        int maxRetries = 5;
        int retryDelaySeconds = 10;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _client.Messages.Create(parameters, cancellationToken: cancellationToken);
                
                // Extract the stop reason as a clean string
                string stopReason = "unknown";
                if (response.StopReason != null)
                {
                    var stopReasonStr = response.StopReason.ToString() ?? "";
                    // Parse "ApiEnum { Json = tool_use }" -> "tool_use"
                    if (stopReasonStr.Contains("Json = "))
                    {
                        var startIdx = stopReasonStr.IndexOf("Json = ") + 7;
                        var endIdx = stopReasonStr.IndexOf(" ", startIdx);
                        if (endIdx == -1) endIdx = stopReasonStr.IndexOf("}", startIdx);
                        if (endIdx > startIdx)
                        {
                            stopReason = stopReasonStr.Substring(startIdx, endIdx - startIdx).Trim();
                        }
                    }
                    else
                    {
                        stopReason = stopReasonStr;
                    }
                }
                
                var result = new LlmResponse
                {
                    Message = response,
                    InputTokens = response.Usage.InputTokens,
                    OutputTokens = response.Usage.OutputTokens,
                    StopReason = stopReason
                };

                Log.Debug("LLM response received. Input tokens: {Input}, Output tokens: {Output}, Stop reason: {StopReason}",
                    result.InputTokens, result.OutputTokens, result.StopReason);

                return result;
            }
            catch (AnthropicRateLimitException ex) when (attempt < maxRetries)
            {
                Log.Warning("Rate limit hit (attempt {Attempt}/{MaxRetries}). Waiting {Delay}s before retry...", 
                    attempt, maxRetries, retryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
                retryDelaySeconds *= 2; // Exponential backoff
            }
            catch (AnthropicBadRequestException ex) when (
                ex.Message.Contains("context", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("too long", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("maximum", StringComparison.OrdinalIgnoreCase))
            {
                Log.Error(ex, "Context overflow detected");
                throw new ContextOverflowException("Context window exceeded", ex);
            }
            catch (AnthropicApiException ex)
            {
                Log.Error(ex, "LLM API error: {Message}", ex.Message);
                throw;
            }
        }

        throw new InvalidOperationException("Max retries exceeded for rate limit");
    }

    /// <summary>
    /// Creates the GetNeighbours tool definition
    /// </summary>
    public static ToolUnion CreateGetNeighboursTool()
    {
        return new Tool
        {
            Name = "GetNeighbours",
            Description = "Get the status of all 8 neighbouring cells around a given position. " +
                         "Returns status for each direction: N, NE, E, SE, S, SW, W, NW. " +
                         "Status can be 'path', 'wall', 'exit', or 'out_of_bounds'. " +
                         "Use this tool to explore the maze and find the path to the exit.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["x"] = JsonDocument.Parse("""{"type": "integer", "description": "X coordinate (column) of the cell to check neighbours for"}""").RootElement,
                    ["y"] = JsonDocument.Parse("""{"type": "integer", "description": "Y coordinate (row) of the cell to check neighbours for"}""").RootElement
                },
                Required = new List<string> { "x", "y" }
            }
        };
    }

    /// <summary>
    /// Simple connection test
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("Testing LLM connection...");
            
            var messages = new List<MessageParam>
            {
                new()
                {
                    Role = Role.User,
                    Content = "Say 'Connection successful!' and nothing else."
                }
            };

            var response = await SendMessageAsync(
                "You are a helpful assistant.",
                messages,
                maxTokens: 50,
                cancellationToken: cancellationToken);

            string? textContent = null;
            foreach (var block in response.Message.Content)
            {
                if (block.TryPickText(out var textBlock))
                {
                    textContent = textBlock.Text;
                    break;
                }
            }

            Log.Information("LLM connection test successful. Response: {Response}", 
                textContent ?? "No text response");
            
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LLM connection test failed");
            return false;
        }
    }
}

/// <summary>
/// Response from the LLM including token usage
/// </summary>
public class LlmResponse
{
    public required Message Message { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public string StopReason { get; set; } = string.Empty;
    
    public long TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// Custom exception for context overflow
/// </summary>
public class ContextOverflowException : Exception
{
    public ContextOverflowException(string message) : base(message) { }
    public ContextOverflowException(string message, Exception inner) : base(message, inner) { }
}
