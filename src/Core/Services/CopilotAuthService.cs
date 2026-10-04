using GitHub.Copilot;
using PodcastMetadataGenerator.Core.Copilot;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Checks for Copilot SDK runtime and authentication status.
/// </summary>
public class CopilotAuthService
{
    private static readonly TimeSpan AuthCheckTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Result of the Copilot readiness check.
    /// </summary>
    public record CopilotStatus(
        bool IsInstalled,
        bool IsTokenSet,
        bool IsAuthenticated,
        string? ErrorMessage,
        string? AuthType = null,
        string? Host = null,
        string? Login = null,
        bool IsCliMissing = false);

    private const string CliMissingMessage =
        "The GitHub Copilot CLI was not found. The app needs it to reach Copilot.";
    
    /// <summary>
    /// Checks if Copilot CLI is ready to use.
    /// Returns detailed status information.
    /// </summary>
    public async Task<CopilotStatus> CheckStatusAsync()
    {
        // Check GH_TOKEN environment variable
        var ghToken = Environment.GetEnvironmentVariable("GH_TOKEN");
        var isTokenSet = !string.IsNullOrEmpty(ghToken);

        // Nothing is bundled to fall back on, so say what is missing rather than letting the
        // SDK report the internal path it looked in.
        if (!CopilotClientFactory.IsCliInstalled)
        {
            return new CopilotStatus(
                IsInstalled: false,
                IsTokenSet: isTokenSet,
                IsAuthenticated: false,
                ErrorMessage: CliMissingMessage,
                IsCliMissing: true);
        }

        var authResult = await CheckCopilotAuthAsync();
        
        return new CopilotStatus(
            IsInstalled: authResult.isRuntimeAvailable,
            IsTokenSet: isTokenSet,
            IsAuthenticated: authResult.isAuthenticated,
            ErrorMessage: authResult.error,
            AuthType: authResult.authType,
            Host: authResult.host,
            Login: authResult.login);
    }
    
    /// <summary>
    /// Quick check if Copilot is ready (installed + authenticated).
    /// </summary>
    public async Task<bool> IsReadyAsync()
    {
        var status = await CheckStatusAsync();
        return status.IsInstalled && (status.IsTokenSet || status.IsAuthenticated);
    }

    private static async Task<(bool isRuntimeAvailable, bool isAuthenticated, string? error, string? authType, string? host, string? login)> CheckCopilotAuthAsync()
    {
        CopilotClient? client = null;
        try
        {
            client = CopilotClientFactory.CreateClient();
            await client.StartAsync().WaitAsync(AuthCheckTimeout);
            
            var authResponse = await client.GetAuthStatusAsync().WaitAsync(AuthCheckTimeout);
            
            if (!authResponse.IsAuthenticated)
            {
                var statusMsg = authResponse.StatusMessage ?? "Not authenticated";
                return (true, false, $"{statusMsg}. Run 'copilot auth login' or set GH_TOKEN environment variable.", null, null, null);
            }
            
            return (true, true, null, authResponse.AuthType, authResponse.Host, authResponse.Login);
        }
        catch (TimeoutException)
        {
            await ForceStopClientAsync(client);
            return (false, false, "Timed out while starting the Copilot SDK runtime. Try again, or set COPILOT_CLI_PATH to a known working Copilot CLI executable.", null, null, null);
        }
        catch (Exception ex)
        {
            return (false, false, $"Failed to check authentication: {ex.Message}", null, null, null);
        }
        finally
        {
            await DisposeClientAsync(client);
        }
    }

    private static async Task DisposeClientAsync(CopilotClient? client)
    {
        if (client == null)
        {
            return;
        }

        try
        {
            await client.DisposeAsync().AsTask().WaitAsync(ShutdownTimeout);
        }
        catch
        {
            await ForceStopClientAsync(client);
        }
    }

    private static async Task ForceStopClientAsync(CopilotClient? client)
    {
        if (client == null)
        {
            return;
        }

        try
        {
            await client.ForceStopAsync().WaitAsync(ShutdownTimeout);
        }
        catch
        {
        }
    }
}
