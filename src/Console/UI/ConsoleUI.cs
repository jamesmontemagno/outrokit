using System.Runtime.InteropServices;
using Spectre.Console;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Console.UI;

/// <summary>
/// Helper methods for Spectre.Console UI patterns.
/// </summary>
public static class ConsoleUI
{
    /// <summary>
    /// Shows the OutroKit banner.
    /// </summary>
    public static void ShowHeader(CopilotAuthService.CopilotStatus? copilotStatus = null)
    {
        AnsiConsole.Clear();
        
        // The wordmark sits under three bands, like the card on outrokit.com.
        var banner = @"
[#f2543a]  ████████████████████████████████████████████████████████████████[/]
[#ffd22e]  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/]
[#6f7ef7]  ────────────────────────────────────────────────────────────────[/]

[bold]   ██████╗ ██╗   ██╗████████╗██████╗  ██████╗ ██╗  ██╗██╗████████╗[/]
[bold]  ██╔═══██╗██║   ██║╚══██╔══╝██╔══██╗██╔═══██╗██║ ██╔╝██║╚══██╔══╝[/]
[bold]  ██║   ██║██║   ██║   ██║   ██████╔╝██║   ██║█████╔╝ ██║   ██║   [/]
[bold]  ██║   ██║██║   ██║   ██║   ██╔══██╗██║   ██║██╔═██╗ ██║   ██║   [/]
[bold]  ╚██████╔╝╚██████╔╝   ██║   ██║  ██║╚██████╔╝██║  ██╗██║   ██║   [/]
[bold]   ╚═════╝  ╚═════╝    ╚═╝   ╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═╝╚═╝   ╚═╝   [/]
";
        AnsiConsole.Markup(banner);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [bold]The episode is done.[/] [bold #f2543a]The label isn't.[/]");
        AnsiConsole.MarkupLine("  [grey]Titles, descriptions, chapters, and subtitles for your episode.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule { Style = Style.Parse("grey") });
        AnsiConsole.WriteLine();
        
        // Show Copilot status if provided
        if (copilotStatus != null)
        {
            ShowCopilotStatus(copilotStatus);
        }
    }
    
    /// <summary>
    /// Tells someone running the original package how to move to the renamed one.
    /// </summary>
    public static void ShowRenamedPackageNotice()
    {
        AnsiConsole.MarkupLine(
            $"[yellow]![/] [bold]This package has a new name: {PackageIdentity.PackageId}.[/] Switch to it to keep getting updates.");
        AnsiConsole.MarkupLine($"  Next time, start the app with: [cyan]dnx {PackageIdentity.PackageId}[/]");
        AnsiConsole.MarkupLine(
            $"  Installed it as a tool? Run [cyan]dotnet tool uninstall -g {PackageIdentity.LegacyPackageId}[/], then [cyan]dotnet tool install -g {PackageIdentity.PackageId}[/], and start it with [cyan]outrokit[/].");
        AnsiConsole.MarkupLine("  [grey]Your settings and downloaded models carry over.[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Shows Copilot SDK runtime status.
    /// </summary>
    public static void ShowCopilotStatus(CopilotAuthService.CopilotStatus status)
    {
        AnsiConsole.MarkupLine("[bold]Copilot Runtime Status[/]");
        AnsiConsole.WriteLine();
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Check")
            .AddColumn("Status");
        
        table.AddRow(
            "Copilot Runtime Available",
            status.IsInstalled ? "[green]✓ Available[/]" : "[red]✗ Unavailable[/]");
        
        table.AddRow(
            "GH_TOKEN Environment Variable",
            status.IsTokenSet ? "[green]✓ Set[/]" : "[grey]○ Not Set[/]");
        
        table.AddRow(
            "Authentication Status",
            status.IsAuthenticated ? "[green]✓ Authenticated[/]" : "[red]✗ Not Authenticated[/]");
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        
        // Show success message if ready
        if (status.IsInstalled && (status.IsTokenSet || status.IsAuthenticated))
        {
            AnsiConsole.MarkupLine("[green]✓ Copilot runtime is ready to use![/]");
            AnsiConsole.WriteLine();
            return;
        }
        
        if (!string.IsNullOrEmpty(status.ErrorMessage))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {status.ErrorMessage}");
            AnsiConsole.WriteLine();
        }
        
        if (status.IsCliMissing)
        {
            var installCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "winget install GitHub.Copilot"
                : "curl -fsSL https://gh.io/copilot-install | bash";
            AnsiConsole.MarkupLine("[yellow]To install the Copilot CLI:[/]");
            AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(installCommand)}[/]");
            AnsiConsole.MarkupLine("  Then sign in with [cyan]copilot login[/] and start OutroKit again.");
            AnsiConsole.MarkupLine("  [dim]Step-by-step guide: https://outrokit.com/#a3[/]");
            AnsiConsole.MarkupLine("  [dim]Already installed? Set COPILOT_CLI_PATH to the copilot executable.[/]");
            AnsiConsole.WriteLine();
        }
        else if (!status.IsInstalled)
        {
            AnsiConsole.MarkupLine("[yellow]To start the Copilot runtime:[/]");
            AnsiConsole.MarkupLine("  [cyan]Restart the app, or set COPILOT_CLI_PATH to a known working Copilot CLI executable.[/]");
            AnsiConsole.WriteLine();
        }
        
        if (status.IsInstalled && !status.IsAuthenticated && !status.IsTokenSet)
        {
            AnsiConsole.MarkupLine("[yellow]To authenticate:[/]");
            AnsiConsole.MarkupLine("  [cyan]copilot auth login[/]");
            AnsiConsole.MarkupLine("  [dim]or set the GH_TOKEN environment variable:[/]");
            AnsiConsole.MarkupLine("  [cyan]export GH_TOKEN=your_github_token[/]");
            AnsiConsole.WriteLine();
        }
    }
    
    /// <summary>
    /// Shows a success message.
    /// </summary>
    public static void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }
    
    /// <summary>
    /// Shows an error message.
    /// </summary>
    public static void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }
    
    /// <summary>
    /// Shows a warning message.
    /// </summary>
    public static void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]![/] {Markup.Escape(message)}");
    }
    
    /// <summary>
    /// Shows an info message.
    /// </summary>
    public static void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }
    
    /// <summary>
    /// Shows a panel with content.
    /// </summary>
    public static void ShowPanel(string title, string content, Color borderColor = default)
    {
        var color = borderColor == default ? Color.Blue : borderColor;
        var separator = BuildSeparator();

        AnsiConsole.Write(new Text(separator, new Style(color)));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        WritePlain(content);
        AnsiConsole.Write(new Text(separator, new Style(color)));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Writes text straight to the terminal without Spectre's word wrapping. Spectre wraps
    /// by inserting real line breaks, which end up in the clipboard when the text is
    /// selected; unwrapped text is soft-wrapped by the terminal and copies as written.
    /// </summary>
    public static void WritePlain(string text)
    {
        var writer = AnsiConsole.Profile.Out.Writer;
        writer.WriteLine(text);
        writer.Flush();
    }
    
    /// <summary>
    /// Shows generated text under a heading in a copy-friendly way.
    /// </summary>
    public static void ShowCopyableBlock(string title, string content)
    {
        var rule = new Rule { Style = Style.Parse("grey") };
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        AnsiConsole.Write(rule);
        WritePlain(content);
        AnsiConsole.Write(rule);
    }
    
    /// <summary>
    /// Copies text to the system clipboard and reports the outcome.
    /// </summary>
    public static async Task<bool> CopyToClipboardAsync(string what, string text)
    {
        try
        {
            await TextCopy.ClipboardService.SetTextAsync(text);
            ShowSuccess($"Copied {what} to clipboard ({text.Length:N0} characters)");
            return true;
        }
        catch (Exception ex)
        {
            var hint = OperatingSystem.IsLinux() ? " (clipboard access on Linux requires xsel)" : "";
            ShowWarning($"Could not copy to clipboard{hint}: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Shows a panel with markup content.
    /// </summary>
    public static void ShowMarkupPanel(string title, string markupContent, Color borderColor = default)
    {
        var color = borderColor == default ? Color.Blue : borderColor;
        var separator = BuildSeparator();

        AnsiConsole.Write(new Text(separator, new Style(color)));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        AnsiConsole.MarkupLine(markupContent);
        AnsiConsole.Write(new Text(separator, new Style(color)));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private static string BuildSeparator()
    {
        var width = Math.Max(20, AnsiConsole.Profile.Width);
        return new string('-', width);
    }
    
    /// <summary>
    /// Prompts user to select from a list of options.
    /// </summary>
    public static T SelectFromList<T>(string title, IEnumerable<T> choices, Func<T, string>? displaySelector = null) where T : notnull
    {
        var prompt = new SelectionPrompt<T>()
            .Title(title)
            .PageSize(10)
            .MoreChoicesText("[grey](Move up/down to see more)[/]")
            .HighlightStyle(Style.Parse("blue"))
            .AddChoices(choices);
        
        if (displaySelector != null)
        {
            prompt.UseConverter(displaySelector);
        }
        
        return AnsiConsole.Prompt(prompt);
    }
    
    /// <summary>
    /// Prompts user to select multiple items from a list.
    /// </summary>
    public static List<T> SelectMultiple<T>(string title, IEnumerable<T> choices, Func<T, string>? displaySelector = null) where T : notnull
    {
        var prompt = new MultiSelectionPrompt<T>()
            .Title(title)
            .PageSize(10)
            .MoreChoicesText("[grey](Move up/down to see more)[/]")
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
            .AddChoices(choices);
        
        if (displaySelector != null)
        {
            prompt.UseConverter(displaySelector);
        }
        
        return AnsiConsole.Prompt(prompt);
    }
    
    /// <summary>
    /// Prompts for text input with optional validation.
    /// </summary>
    public static string AskText(string prompt, string? defaultValue = null, Func<string, bool>? validator = null, string? validationMessage = null)
    {
        var textPrompt = new TextPrompt<string>(prompt);
        
        if (defaultValue != null)
        {
            textPrompt.DefaultValue(defaultValue);
        }
        
        if (validator != null)
        {
            textPrompt.Validate(value =>
            {
                if (validator(value))
                    return ValidationResult.Success();
                return ValidationResult.Error(validationMessage ?? "Invalid input");
            });
        }
        
        return AnsiConsole.Prompt(textPrompt);
    }
    
    /// <summary>
    /// Prompts for file path with existence validation and file browser option.
    /// Handles drag-and-drop paths that may have quotes or escape characters.
    /// </summary>
    public static string AskFilePath(
        string prompt,
        bool mustExist = true,
        string? startDirectory = null,
        FileDiscoveryType discoveryType = FileDiscoveryType.Transcript)
    {
        while (true)
        {
            // Offer browse option or direct input
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(prompt)
                    .AddChoices("📂 Browse for file", "⌨️ Type or paste path"));
            
            string path;
            
            if (choice.StartsWith("📂"))
            {
                var browsedPath = BrowseForFile(startDirectory, discoveryType);
                if (browsedPath == null)
                    continue; // User cancelled, show menu again
                path = browsedPath;
            }
            else
            {
                path = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter file path (drag & drop supported):")
                        .AllowEmpty());
                
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                
                // Clean up drag-and-drop paths
                path = CleanFilePath(path);
            }
            
            if (mustExist && !File.Exists(path))
            {
                ShowError($"File not found: {path}");
                continue;
            }
            
            if (!mustExist || File.Exists(path))
            {
                return path;
            }
        }
    }

    /// <summary>
    /// Prompts for where to save a file, confirming before overwriting an existing one.
    /// </summary>
    /// <param name="extensions">
    /// The extensions the file may have, such as ".srt". The first is added when the path has none of them.
    /// </param>
    /// <param name="validate">Returns why a path cannot be used, or null when it can.</param>
    public static string AskSaveFilePath(
        string prompt,
        string defaultPath,
        IReadOnlyList<string> extensions,
        Func<string, string?>? validate = null)
    {
        while (true)
        {
            // File names routinely contain [brackets], which Spectre would parse as markup,
            // and it does not escape a prompt's default value itself.
            var path = AnsiConsole.Prompt(
                new TextPrompt<string>($"{prompt} [green]({Markup.Escape(defaultPath)})[/]:")
                    .DefaultValue(defaultPath)
                    .HideDefaultValue());
            path = CleanFilePath(path);

            if (extensions.Count > 0
                && !extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                path += extensions[0];
            }

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                ShowError($"Directory not found: {directory}");
                continue;
            }

            if (validate?.Invoke(fullPath) is { } problem)
            {
                ShowError(problem);
                continue;
            }

            if (!File.Exists(fullPath)
                || AnsiConsole.Confirm($"Overwrite '{Markup.Escape(Path.GetFileName(fullPath))}'?", false))
            {
                return fullPath;
            }
        }
    }
    
    /// <summary>
    /// Cleans a file path that may have been drag-and-dropped.
    /// Handles quotes, escaped spaces, and other common issues.
    /// </summary>
    private static string CleanFilePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        
        // Trim whitespace
        path = path.Trim();
        
        // Remove surrounding quotes (single or double)
        if ((path.StartsWith('"') && path.EndsWith('"')) ||
            (path.StartsWith('\'') && path.EndsWith('\'')))
        {
            path = path[1..^1];
        }
        
        // Handle escaped spaces (\ followed by space)
        path = path.Replace("\\ ", " ");
        
        // Handle other common escape sequences from terminal
        path = path.Replace("\\(", "(")
                   .Replace("\\)", ")")
                   .Replace("\\[", "[")
                   .Replace("\\]", "]")
                   .Replace("\\'", "'");
        
        return path;
    }
    
    /// <summary>
    /// Simple file browser using selection prompts.
    /// </summary>
    private static string? BrowseForFile(
        string? startDirectory = null,
        FileDiscoveryType discoveryType = FileDiscoveryType.Transcript)
    {
        var currentDir = startDirectory ?? Environment.CurrentDirectory;
        
        // Ensure directory exists
        if (!Directory.Exists(currentDir))
        {
            currentDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        
        while (true)
        {
            AnsiConsole.MarkupLine($"[grey]Current: {Markup.Escape(currentDir)}[/]");
            
            var items = new List<string> { "📁 .." };
            
            try
            {
                // Add directories
                var dirs = Directory.GetDirectories(currentDir)
                    .Select(d => new DirectoryInfo(d))
                    .Where(d => !d.Name.StartsWith('.')) // Hide hidden directories
                    .OrderBy(d => d.Name)
                    .Select(d => $"📁 {d.Name}");
                items.AddRange(dirs);
                
                var files = Directory.GetFiles(currentDir)
                    .Select(f => new FileInfo(f))
                    .Where(f => !f.Name.StartsWith('.') && IsDiscoverableFile(f.Name, discoveryType))
                    .OrderBy(f => f.Name)
                    .Select(f => $"📄 {f.Name}");
                items.AddRange(files);
            }
            catch (UnauthorizedAccessException)
            {
                ShowError("Access denied to this directory");
                currentDir = Directory.GetParent(currentDir)?.FullName ?? currentDir;
                continue;
            }
            
            items.Add("❌ Cancel");
            
            // Names are escaped for display only; brackets in a file or folder name are not markup.
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Select a file or navigate:[/]")
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up/down to see more)[/]")
                    .HighlightStyle(Style.Parse("blue"))
                    .UseConverter(Markup.Escape)
                    .AddChoices(items));
            
            if (selection == "❌ Cancel")
            {
                return null;
            }
            else if (selection == "📁 ..")
            {
                var parent = Directory.GetParent(currentDir);
                if (parent != null)
                {
                    currentDir = parent.FullName;
                }
            }
            else if (selection.StartsWith("📁 "))
            {
                var dirName = selection[3..]; // Remove "📁 " prefix
                currentDir = Path.Combine(currentDir, dirName);
            }
            else if (selection.StartsWith("📄 "))
            {
                var fileName = selection[3..]; // Remove "📄 " prefix
                return Path.Combine(currentDir, fileName);
            }
        }
    }
    
    /// <summary>
    /// Checks whether the file browser should list a file for the kind of input being picked.
    /// </summary>
    private static bool IsDiscoverableFile(string fileName, FileDiscoveryType discoveryType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return discoveryType switch
        {
            FileDiscoveryType.Transcript => ext is ".txt" or ".srt" or ".vtt" or ".json" or ".md" or ".csv",
            FileDiscoveryType.Media => MediaTranscriptService.HasMediaExtension(fileName),
            FileDiscoveryType.Video => MediaTranscriptService.HasVideoExtension(fileName),
            FileDiscoveryType.Captions => CaptionBurnService.HasCaptionExtension(fileName),
            _ => false
        };
    }
    
    /// <summary>
    /// Shows a table of titles for selection.
    /// </summary>
    public static string? SelectTitle(List<string> titles)
    {
        if (titles.Count == 0) return null;
        
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .Title("[bold]Generated Titles[/]")
            .AddColumn("#", col => col.Centered())
            .AddColumn("Title");
        
        for (int i = 0; i < titles.Count; i++)
        {
            table.AddRow($"[blue]{i + 1}[/]", Markup.Escape(titles[i]));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a title:")
                .AddChoices(titles.Concat(["[grey]Skip selection[/]"])));
        
        return choice.StartsWith("[grey]") ? null : choice;
    }
    
    /// <summary>
    /// Prompts for a title style, listing the default first so Enter accepts it.
    /// </summary>
    public static TitleStyle SelectTitleStyle(TitleStyle defaultStyle, string title = "Select a title style:")
    {
        var styles = TitleStyles.All
            .OrderByDescending(style => style == defaultStyle)
            .ToList();
        
        return SelectFromList(
            title,
            styles,
            style =>
            {
                var suffix = style == defaultStyle ? " [green](default)[/]" : "";
                return $"{Markup.Escape(style.GetDisplayName())}{suffix} [grey]- {Markup.Escape(style.GetDescription())}[/]";
            });
    }
    
    /// <summary>
    /// Prompts for a caption text size, listing the default first so Enter accepts it.
    /// </summary>
    public static CaptionSize SelectCaptionSize(CaptionSize defaultSize, string title = "Caption text size:")
    {
        return SelectFromList(
            title,
            CaptionStyles.AllSizes.OrderByDescending(size => size == defaultSize).ToList(),
            size =>
            {
                var suffix = size == defaultSize ? " [green](default)[/]" : "";
                return $"{Markup.Escape(size.GetDisplayName())}{suffix} [grey]- {Markup.Escape(size.GetDescription())}[/]";
            });
    }

    /// <summary>
    /// Prompts for where captions sit in the picture, listing the default first so Enter accepts it.
    /// </summary>
    public static CaptionPosition SelectCaptionPosition(
        CaptionPosition defaultPosition,
        string title = "Caption position:")
    {
        return SelectFromList(
            title,
            CaptionStyles.AllPositions.OrderByDescending(position => position == defaultPosition).ToList(),
            position =>
            {
                var suffix = position == defaultPosition ? " [green](default)[/]" : "";
                return $"{Markup.Escape(position.GetDisplayName())}{suffix} [grey]- {Markup.Escape(position.GetDescription())}[/]";
            });
    }

    /// <summary>
    /// Prompts for a caption appearance, listing the default first so Enter accepts it.
    /// </summary>
    public static CaptionAppearance SelectCaptionAppearance(
        CaptionAppearance defaultAppearance,
        string title = "Caption appearance:")
    {
        return SelectFromList(
            title,
            CaptionStyles.AllAppearances.OrderByDescending(appearance => appearance == defaultAppearance).ToList(),
            appearance =>
            {
                var suffix = appearance == defaultAppearance ? " [green](default)[/]" : "";
                return $"{Markup.Escape(appearance.GetDisplayName())}{suffix} [grey]- {Markup.Escape(appearance.GetDescription())}[/]";
            });
    }
    
    /// <summary>
    /// Prompts for which description lengths to generate, preselecting the supplied lengths.
    /// </summary>
    public static List<DescriptionLength> SelectDescriptionLengths(
        IReadOnlyCollection<DescriptionLength>? preselected = null,
        string title = "Which descriptions would you like to generate?")
    {
        var selected = preselected is { Count: > 0 }
            ? preselected
            : Enum.GetValues<DescriptionLength>();
        
        var prompt = new MultiSelectionPrompt<DescriptionLength>()
            .Title(title)
            .PageSize(10)
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
            .UseConverter(length =>
            {
                var hint = length switch
                {
                    DescriptionLength.Short => "2-3 sentences",
                    DescriptionLength.Medium => "1-2 paragraphs",
                    DescriptionLength.Long => "3-4 paragraphs",
                    _ => ""
                };
                
                return $"{length} [grey]- {hint}[/]";
            });
        
        foreach (var length in Enum.GetValues<DescriptionLength>())
        {
            var item = prompt.AddChoice(length);
            if (selected.Contains(length))
            {
                item.Select();
            }
        }
        
        return AnsiConsole.Prompt(prompt);
    }
    
    /// <summary>
    /// Shows descriptions in a formatted way.
    /// </summary>
    public static void ShowDescriptions(Dictionary<DescriptionLength, string> descriptions)
    {
        foreach (var (length, description) in descriptions)
        {
            var color = length switch
            {
                DescriptionLength.Short => Color.Green,
                DescriptionLength.Medium => Color.Yellow,
                DescriptionLength.Long => Color.Blue,
                _ => Color.White
            };
            
            ShowPanel($"{length} Description", description, color);
        }
    }

    public enum FileDiscoveryType
    {
        Transcript,
        Media,
        Video,
        Captions
    }
    
    /// <summary>
    /// Shows chapters in a formatted table.
    /// </summary>
    public static void ShowChapters(List<Chapter> chapters)
    {
        if (chapters.Count == 0)
        {
            ShowWarning("No chapters generated.");
            return;
        }
        
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.Green)
            .Title("[bold]YouTube Chapters[/]")
            .AddColumn("Timestamp", col => col.Centered())
            .AddColumn("Title");
        
        foreach (var chapter in chapters)
        {
            table.AddRow($"[blue]{Markup.Escape(chapter.Timestamp)}[/]", Markup.Escape(chapter.Title));
        }
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        
        // Also show copy-paste format
        AnsiConsole.MarkupLine("[dim]Copy-paste format for YouTube:[/]");
        var rule = new Rule() { Style = Style.Parse("grey") };
        AnsiConsole.Write(rule);
        
        foreach (var chapter in chapters)
        {
            WritePlain($"{chapter.Timestamp} {chapter.Title}");
        }
        
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Shows transcript info.
    /// </summary>
    public static void ShowTranscriptInfo(Transcript transcript)
    {
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .HideHeaders()
            .AddColumn("Property")
            .AddColumn("Value");
        
        table.AddRow("[blue]File[/]", Markup.Escape(Path.GetFileName(transcript.FilePath)));
        table.AddRow("[blue]Format[/]", transcript.Format.ToString());
        
        if (transcript.HasTimestamps)
        {
            table.AddRow("[blue]Duration[/]", $"{transcript.DurationMinutes:F1} minutes");
            table.AddRow("[blue]Segments[/]", transcript.Segments.Count.ToString());
        }
        else
        {
            // For plain text, show word count and estimated duration
            var wordCount = transcript.RawContent.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
            table.AddRow("[blue]Words[/]", $"{wordCount:N0}");
            table.AddRow("[blue]Est. Duration[/]", $"~{transcript.DurationMinutes:F0} minutes");
            table.AddRow("[grey]Note[/]", "[grey]Plain text - no timestamps detected[/]");
        }
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Shows a live display for streaming text.
    /// </summary>
    public static async Task<string> ShowStreamingResponseAsync(
        string title,
        Func<Action<string>, Task<string>> generator)
    {
        var result = string.Empty;
        var panel = new Panel("")
        {
            Header = new PanelHeader($"[bold] {Markup.Escape(title)} [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(1, 0),
            Expand = true
        };
        
        await AnsiConsole.Live(panel)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                result = await generator(chunk =>
                {
                    panel = new Panel(Markup.Escape(result + chunk))
                    {
                        Header = new PanelHeader($"[bold] {Markup.Escape(title)} [/]"),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Blue),
                        Padding = new Padding(1, 0),
                        Expand = true
                    };
                    ctx.UpdateTarget(panel);
                });
            });
        
        return result;
    }
    
    /// <summary>
    /// Shows the most recent entries from <see cref="WhatsNew"/>.
    /// </summary>
    public static void ShowWhatsNew(int maxReleases = 3)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]✨ What's New[/]")
        {
            Justification = Justify.Left,
            Style = Style.Parse("blue")
        });

        foreach (var release in WhatsNew.Releases.Take(maxReleases))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold blue]{Markup.Escape(release.Heading)}[/]");
            AnsiConsole.WriteLine();

            // A grid keeps wrapped description lines aligned under their title.
            var grid = new Grid()
                .AddColumn(new GridColumn { NoWrap = true, Padding = new Padding(2, 0, 2, 0) })
                .AddColumn();

            foreach (var item in release.Items)
            {
                grid.AddRow(
                    new Markup(Markup.Escape(item.Icon)),
                    new Rows(
                        new Markup($"[bold]{Markup.Escape(item.Title)}[/]"),
                        new Markup($"[grey]{Markup.Escape(item.Description)}[/]"),
                        Text.Empty));
            }

            AnsiConsole.Write(grid);
        }
    }

    /// <summary>
    /// Waits for user to press any key.
    /// </summary>
    public static void WaitForKey(string message = "Press any key to continue...")
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
        System.Console.ReadKey(true);
    }
}
