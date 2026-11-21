using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Copilot;

#region Read in transcript

var transcriptPath = args.Length > 0 ? args[0] : GetDefaultTranscriptPath();

if (string.IsNullOrWhiteSpace(transcriptPath))
{
    Console.WriteLine("Usage: dotnet run --project samples/YouTubeChaptersDemo -- <transcript.srt>");
    Console.WriteLine("No transcript argument was provided, and no default transcript was found.");
    Console.WriteLine("Searched upward from current directory and app base directory for:");
    Console.WriteLine("  data/mergeconflict498.srt");
    Console.WriteLine("  src/Console/mergeconflict498.srt");
    return;
}

if (args.Length == 0)
{
    Console.WriteLine($"No transcript specified. Using default: {transcriptPath}");
}

if (!File.Exists(transcriptPath))
{
    Console.WriteLine($"Transcript file not found: {transcriptPath}");
    return;
}

var transcriptForPrompt = await LoadSrtAsTimestampedTranscriptAsync(transcriptPath);
if (string.IsNullOrWhiteSpace(transcriptForPrompt))
{
    Console.WriteLine("No transcript content was parsed from the SRT file.");
    return;
}

#endregion

await using var client = new CopilotClient(new CopilotClientOptions { Mode = CopilotClientMode.CodingAgent });
await client.StartAsync();

var authStatus = await client.GetAuthStatusAsync();
if (!authStatus.IsAuthenticated)
{
    Console.WriteLine("Copilot is not authenticated. Run 'copilot auth login' and try again.");
    return;
}

var selectedModel = await PickModelAsync(client, preferredModel: "gpt-5.4-mini");
if (string.IsNullOrWhiteSpace(selectedModel))
{
    Console.WriteLine("No Copilot models are available in this environment.");
    return;
}

Console.WriteLine($"Using model: {selectedModel}");

var session = await client.CreateSessionAsync(new CreateSessionOptions
{
    Model = selectedModel,
    Events = new() { Streaming = true },
    Permissions = new() { OnRequest = PermissionHandler.ApproveAll },
    SystemMessage = new SystemMessageConfig
    {
        Mode = SystemMessageMode.Replace,
        Content = "You are a podcast editor that creates YouTube chapter markers from transcripts."
    }
});

await using (session)
{
    var responseBuilder = new StringBuilder();
    var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    session.On<SessionEvent>(evt =>
    {
        switch (evt)
        {
            case AssistantMessageDeltaEvent delta:
                var chunk = delta.Data.DeltaContent ?? string.Empty;
                responseBuilder.Append(chunk);
                Console.Write(chunk);
                break;

            case AssistantMessageEvent message:
                if (responseBuilder.Length == 0)
                {
                    responseBuilder.Append(message.Data.Content ?? string.Empty);
                }
                break;

            case SessionErrorEvent error:
                done.TrySetException(new InvalidOperationException(error.Data.Message));
                break;

            case SessionIdleEvent:
                done.TrySetResult();
                break;
        }
    });

        var prompt = $"""
Create YouTube chapters from this timestamped transcript.

Rules:
- Only create chapters for major topic shifts.
- First chapter must be 00:00.
- Use the segment START timestamp for each chapter.
- Return only chapter lines in one of these formats:
    MM:SS Title Here
    HH:MM:SS Title Here
- Do not include numbering, bullets, or commentary.

Transcript:
{transcriptForPrompt}
""";

    Console.WriteLine();
    Console.WriteLine("\nStreaming model output...\n");

    await session.SendAsync(new MessageOptions { Prompt = prompt });
    await done.Task;

    var chapters = ParseChapterLines(responseBuilder.ToString());

    Console.WriteLine("\n\nYouTube Chapters:\n");
    foreach (var chapter in chapters)
    {
        Console.WriteLine($"{chapter.Timestamp} {chapter.Title}");
    }
}

#region Demo-Friendly Helpers

static string? GetDefaultTranscriptPath()
{
    var candidates = new[]
    {
        Path.Combine("data", "mergeconflict498.srt"),
        Path.Combine("src", "Console", "mergeconflict498.srt")
    };

    var roots = GetSearchRoots(Environment.CurrentDirectory)
        .Concat(GetSearchRoots(AppContext.BaseDirectory))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    foreach (var root in roots)
    {
        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, candidate));
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
    }

    return null;
}

static IEnumerable<string> GetSearchRoots(string startPath)
{
    if (string.IsNullOrWhiteSpace(startPath))
    {
        yield break;
    }

    DirectoryInfo? current = new DirectoryInfo(Path.GetFullPath(startPath));
    var depth = 0;

    // Limit depth so debug output paths are covered without scanning too broadly.
    while (current is not null && depth < 10)
    {
        yield return current.FullName;
        current = current.Parent;
        depth++;
    }
}

static async Task<string> PickModelAsync(CopilotClient client, string preferredModel)
{
    var models = await client.ListModelsAsync();
    var all = models.ToList();

    if (all.Count == 0)
    {
        return string.Empty;
    }

    var defaultModel = all.FirstOrDefault(m =>
        string.Equals(m.Id, preferredModel, StringComparison.OrdinalIgnoreCase));
    var defaultIndex = defaultModel is null ? 0 : all.IndexOf(defaultModel);

    Console.WriteLine("\nPick a model:");
    for (var i = 0; i < all.Count; i++)
    {
        var model = all[i];
        var label = string.IsNullOrWhiteSpace(model.Name) ? model.Id : $"{model.Name} ({model.Id})";
        var suffix = i == defaultIndex ? " [default]" : string.Empty;
        Console.WriteLine($"{i + 1}. {label}{suffix}");
    }

    Console.Write($"Model number or id (Enter = {all[defaultIndex].Id}): ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(input))
    {
        return all[defaultIndex].Id;
    }

    if (int.TryParse(input, out var selectedIndex) && selectedIndex >= 1 && selectedIndex <= all.Count)
    {
        return all[selectedIndex - 1].Id;
    }

    var byId = all.FirstOrDefault(m =>
        string.Equals(m.Id, input, StringComparison.OrdinalIgnoreCase));

    if (byId is not null)
    {
        return byId.Id;
    }

    Console.WriteLine($"Invalid selection. Using default: {all[defaultIndex].Id}");
    return all[defaultIndex].Id;
}

#region Parsing Helpers

static async Task<string> LoadSrtAsTimestampedTranscriptAsync(string filePath)
{
    var lines = await File.ReadAllLinesAsync(filePath);

    var timestampRegex = new Regex(
        @"(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})",
        RegexOptions.Compiled);

    var formatted = new List<string>();

    for (var i = 0; i < lines.Length; i++)
    {
        var match = timestampRegex.Match(lines[i]);
        if (!match.Success)
        {
            continue;
        }

        var start = SrtToYouTube(match.Groups[1].Value);
        var end = SrtToYouTube(match.Groups[2].Value);

        var textLines = new List<string>();
        for (var j = i + 1; j < lines.Length; j++)
        {
            if (string.IsNullOrWhiteSpace(lines[j]))
            {
                i = j;
                break;
            }

            if (timestampRegex.IsMatch(lines[j]))
            {
                i = j - 1;
                break;
            }

            if (int.TryParse(lines[j].Trim(), out _))
            {
                continue;
            }

            textLines.Add(lines[j].Trim());
        }

        var segmentText = string.Join(" ", textLines).Trim();
        if (!string.IsNullOrWhiteSpace(segmentText))
        {
            formatted.Add($"[{start} - {end}] {segmentText}");
        }
    }

    return string.Join("\n", formatted);
}

static string SrtToYouTube(string srtTimestamp)
{
    var parts = srtTimestamp.Split([',']);
    var hhmmss = parts[0];
    var time = TimeSpan.Parse(hhmmss);

    if (time.Hours > 0)
    {
        return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
    }

    return $"{time.Minutes:D2}:{time.Seconds:D2}";
}


static List<(string Timestamp, string Title)> ParseChapterLines(string response)
{
    var chapterRegex = new Regex(@"^(\d{1,2}:\d{2}(?::\d{2})?)\s+(.+)$", RegexOptions.Multiline);
    var chapters = new List<(string Timestamp, string Title)>();

    foreach (Match match in chapterRegex.Matches(response))
    {
        var timestamp = NormalizeTimestamp(match.Groups[1].Value.Trim());
        var title = match.Groups[2].Value.Trim();

        if (!string.IsNullOrWhiteSpace(title))
        {
            chapters.Add((timestamp, title));
        }
    }

    if (chapters.Count > 0 && chapters[0].Timestamp is not ("00:00" or "00:00:00"))
    {
        chapters.Insert(0, ("00:00", "Introduction"));
    }

    return chapters;
}

static string NormalizeTimestamp(string raw)
{
    var parts = raw.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (parts.Length == 2 &&
        int.TryParse(parts[0], out var minutes) &&
        int.TryParse(parts[1], out var seconds))
    {
        return $"{minutes:D2}:{seconds:D2}";
    }

    if (parts.Length == 3 &&
        int.TryParse(parts[0], out var hours) &&
        int.TryParse(parts[1], out var mins) &&
        int.TryParse(parts[2], out var secs))
    {
        return $"{hours:D2}:{mins:D2}:{secs:D2}";
    }

    return raw;
}

#endregion

#endregion
