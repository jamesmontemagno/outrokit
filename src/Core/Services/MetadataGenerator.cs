using System.Text.RegularExpressions;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using PodcastMetadataGenerator.Core.Copilot;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Prompts;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Generates podcast metadata using the GitHub Copilot SDK.
/// </summary>
public partial class MetadataGenerator : IAsyncDisposable
{
    private readonly AppSettings _settings;
    private readonly Func<PermissionRequest, PermissionInvocation, Task<bool>>? _permissionApprovalCallback;
    private CopilotClient? _client;
    private bool _isInitialized;
    private string? _resolvedModel;
    
    [GeneratedRegex(@"^\d+\.\s*", RegexOptions.Multiline)]
    private static partial Regex NumberedListRegex();
    
    [GeneratedRegex(@"^(\d{1,2}:\d{2}(?::\d{2})?)\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex ChapterLineRegex();
    
    public MetadataGenerator(
        AppSettings settings,
        Func<PermissionRequest, PermissionInvocation, Task<bool>>? permissionApprovalCallback = null)
    {
        _settings = settings;
        _permissionApprovalCallback = permissionApprovalCallback;
    }
    
    /// <summary>
    /// Initializes the Copilot client.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            _client = CopilotClientFactory.CreateClient();
            await _client.StartAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize Copilot client: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Generates title suggestions for the podcast episode with streaming support.
    /// </summary>
    /// <param name="style">Title style for this run; falls back to the default in settings when null.</param>
    public async Task<List<string>> GenerateTitlesAsync(
        Transcript transcript,
        TitleStyle? style = null,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();
        
        var response = await SendPromptWithStreamingAsync(
            PromptTemplates.TitleSystemPrompt,
            PromptTemplates.GetTitleUserPrompt(transcript.GetFullText(), _settings, style),
            onChunk,
            cancellationToken);
        
        // Parse numbered list response
        var titles = response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => NumberedListRegex().Replace(line.Trim(), ""))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(_settings.TitleCount)
            .ToList();
        
        return titles;
    }
    
    /// <summary>
    /// Generates a description of the specified length with streaming support.
    /// </summary>
    public async Task<string> GenerateDescriptionAsync(
        Transcript transcript,
        DescriptionLength length,
        string? selectedTitle = null,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();
        
        var response = await SendPromptWithStreamingAsync(
            PromptTemplates.DescriptionSystemPrompt,
            PromptTemplates.GetDescriptionUserPrompt(
                transcript.GetFullText(), 
                length, 
                _settings,
                selectedTitle),
            onChunk,
            cancellationToken);
        
        return response.Trim();
    }
    
    /// <summary>
    /// Generates all three description lengths.
    /// </summary>
    public async Task<Dictionary<DescriptionLength, string>> GenerateAllDescriptionsAsync(
        Transcript transcript,
        string? selectedTitle = null,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        var descriptions = new Dictionary<DescriptionLength, string>();
        
        foreach (var length in Enum.GetValues<DescriptionLength>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            descriptions[length] = await GenerateDescriptionAsync(
                transcript, length, selectedTitle, onChunk, cancellationToken);
        }
        
        return descriptions;
    }
    
    /// <summary>
    /// Generates YouTube chapters for the episode with streaming support.
    /// </summary>
    public async Task<List<Chapter>> GenerateChaptersAsync(
        Transcript transcript,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();
        
        var response = await SendPromptWithStreamingAsync(
            PromptTemplates.ChapterSystemPrompt,
            PromptTemplates.GetChapterUserPrompt(
                transcript.GetTextWithTimestamps(),
                transcript.DurationSeconds,
                _settings),
            onChunk,
            cancellationToken);
        
        // Parse chapter response
        var chapters = new List<Chapter>();
        var matches = ChapterLineRegex().Matches(response);
        
        foreach (Match match in matches)
        {
            var timestamp = NormalizeTimestamp(match.Groups[1].Value);
            var title = match.Groups[2].Value.Trim();
            
            if (!string.IsNullOrEmpty(title))
            {
                chapters.Add(new Chapter
                {
                    Timestamp = timestamp,
                    Title = title
                });
            }
        }
        
        // Ensure first chapter starts at 00:00
        if (chapters.Count > 0 && chapters[0].Timestamp != "00:00" && chapters[0].Timestamp != "00:00:00")
        {
            chapters.Insert(0, new Chapter
            {
                Timestamp = "00:00",
                Title = "Introduction"
            });
        }
        
        return chapters;
    }
    
    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized || _client == null)
        {
            await InitializeAsync();
        }
    }
    
    private async Task<string> SendPromptWithStreamingAsync(
        string systemPrompt,
        string userPrompt,
        Action<string>? onChunk,
        CancellationToken cancellationToken)
    {
        if (_client == null)
            throw new InvalidOperationException("Client not initialized");
        
        // Sanitize the user prompt to avoid serialization issues
        // Remove or replace any characters that might cause JSON serialization problems
        var sanitizedPrompt = SanitizeForJson(userPrompt);
        
        CopilotSession? session = null;
        try
        {
            _settings.Model = await ResolveModelAsync(_client, cancellationToken);

            if (string.IsNullOrWhiteSpace(_settings.Model))
            {
                throw new InvalidOperationException("No models were returned by the Copilot SDK.");
            }

            // Note: Streaming set to true causes serialization issues with large prompts
            // in some SDK versions, so we use streaming only when an onChunk handler is provided
            session = await _client.CreateSessionAsync(new SessionConfig
            {
                ClientName = CopilotClientFactory.ClientName,
                Model = _settings.Model,
                OnPermissionRequest = HandlePermissionRequestAsync,
                Streaming = true, // Always use streaming for better responsiveness
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = SanitizeForJson(systemPrompt)
                }
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create session (model: {_settings.Model}): {ex.Message}", ex);
        }
        
        await using var _ = session;
        
        var responseBuilder = new System.Text.StringBuilder();
        var done = new TaskCompletionSource();
        
        // Register cancellation
        await using var registration = cancellationToken.Register(() => 
        {
            done.TrySetCanceled(cancellationToken);
        });
        
        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    responseBuilder.Append(delta.Data.DeltaContent);
                    onChunk?.Invoke(delta.Data.DeltaContent ?? "");
                    break;
                case AssistantMessageEvent msg:
                    if (onChunk == null)
                    {
                        responseBuilder.Append(msg.Data.Content);
                    }
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent error:
                    done.TrySetException(new Exception($"Session error: {error.Data.Message}"));
                    break;
            }
        });
        
        try
        {
            await session.SendAsync(new MessageOptions { Prompt = sanitizedPrompt });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to send message (prompt length: {sanitizedPrompt.Length} chars): {ex.Message}", ex);
        }
        
        await done.Task;
        
        return responseBuilder.ToString();
    }

    /// <summary>
    /// Validates the configured model against the running client's model list.
    /// The result is reused until the configured model changes, so a multi-prompt
    /// run does not re-list models (or start another runtime) for every prompt.
    /// </summary>
    private async Task<string> ResolveModelAsync(CopilotClient client, CancellationToken cancellationToken)
    {
        var requested = _settings.Model?.Trim();

        if (!string.IsNullOrWhiteSpace(_resolvedModel) &&
            string.Equals(requested, _resolvedModel, StringComparison.OrdinalIgnoreCase))
        {
            return _resolvedModel;
        }

        List<ModelInfo> models;
        try
        {
            models = (await client.ListModelsAsync(cancellationToken)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            models = [];
        }

        var resolved = AvailableModels.SelectModel(models, requested);
        _resolvedModel = models.Count > 0 ? resolved : null;
        return resolved;
    }

    public static string DescribePermissionRequest(PermissionRequest request, PermissionInvocation invocation)
    {
        var sessionInfo = $"Session: {invocation.SessionId}";

        string FormatBase(string? toolCallId, string extraDetails) =>
            $"Kind: {request.Kind}\n{sessionInfo}\nTool Call: {toolCallId ?? "N/A"}\n{extraDetails}";

        return request switch
        {
            PermissionRequestShell shell =>
                FormatBase(shell.ToolCallId, $"Command: {shell.FullCommandText ?? "N/A"}\nIntention: {shell.Intention ?? "N/A"}"),
            PermissionRequestWrite write =>
                FormatBase(write.ToolCallId, $"File: {write.FileName ?? "N/A"}\nIntention: {write.Intention ?? "N/A"}"),
            PermissionRequestRead read =>
                FormatBase(read.ToolCallId, $"Path: {read.Path ?? "N/A"}\nIntention: {read.Intention ?? "N/A"}"),
            PermissionRequestUrl url =>
                FormatBase(url.ToolCallId, $"URL: {url.Url ?? "N/A"}\nIntention: {url.Intention ?? "N/A"}"),
            PermissionRequestMemory memory =>
                FormatBase(memory.ToolCallId, $"Subject: {memory.Subject ?? "N/A"}"),
            PermissionRequestMcp mcp =>
                FormatBase(mcp.ToolCallId, $"Server: {mcp.ServerName ?? "N/A"}\nTool: {mcp.ToolName ?? "N/A"}"),
            PermissionRequestCustomTool customTool =>
                FormatBase(customTool.ToolCallId, $"Tool: {customTool.ToolName ?? "N/A"}\nDescription: {customTool.ToolDescription ?? "N/A"}"),
            PermissionRequestHook hook =>
                FormatBase(hook.ToolCallId, $"Hook: {hook.ToolName ?? "N/A"}\nMessage: {hook.HookMessage ?? "N/A"}"),
            _ =>
                $"Kind: {request.Kind}\n{sessionInfo}"
        };
    }

#pragma warning disable GHCP001
    private async Task<PermissionDecision> HandlePermissionRequestAsync(
        PermissionRequest request,
        PermissionInvocation invocation)
    {
        if (_permissionApprovalCallback == null)
        {
            return PermissionDecision.UserNotAvailable();
        }

        var approved = await _permissionApprovalCallback(request, invocation);

        return approved
            ? PermissionDecision.ApproveOnce()
            : PermissionDecision.Reject();
    }
        #pragma warning restore GHCP001
    
    /// <summary>
    /// Sanitizes text to avoid JSON serialization issues.
    /// </summary>
    private static string SanitizeForJson(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        // Replace problematic characters that might cause JSON serialization issues
        var result = new System.Text.StringBuilder(text.Length);
        
        foreach (var c in text)
        {
            // Replace control characters (except standard whitespace)
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            {
                result.Append(' ');
            }
            // Keep other characters as-is
            else
            {
                result.Append(c);
            }
        }
        
        return result.ToString();
    }
    
    /// <summary>
    /// Normalizes timestamp to MM:SS or HH:MM:SS format.
    /// </summary>
    private static string NormalizeTimestamp(string timestamp)
    {
        var parts = timestamp.Split(':');
        
        return parts.Length switch
        {
            2 => $"{int.Parse(parts[0]):D2}:{int.Parse(parts[1]):D2}",
            3 => $"{int.Parse(parts[0]):D2}:{int.Parse(parts[1]):D2}:{int.Parse(parts[2]):D2}",
            _ => timestamp
        };
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
        _isInitialized = false;
        _resolvedModel = null;
        GC.SuppressFinalize(this);
    }
}
