using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitHub.Copilot;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Console.Cli;

public sealed record CliPermissionRequest(string Id, string Description);

public sealed class CliPermissionRequiredException(CliPermissionRequest request)
    : Exception("Copilot requested permission. Ask the user to approve or deny this request before retrying.")
{
    public CliPermissionRequest Request { get; } = request;
}

public sealed class CliPermissionGate(
    IEnumerable<string> approvals, CancellationTokenSource cancellation, IEnumerable<string>? denials = null)
{
    private readonly HashSet<string> _approvals = new(approvals, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _denials = new(denials ?? [], StringComparer.OrdinalIgnoreCase);
    private readonly object _decisionLock = new();
    public CliPermissionRequiredException? Pending { get; private set; }

    public Task<bool> DecideAsync(PermissionRequest request, PermissionInvocation invocation)
    {
        var data = JsonSerializer.SerializeToNode(request, request.GetType())!.AsObject();
        // Invocation IDs change on retry; the action and all its other details must still match.
        data.Remove("toolCallId");
        data.Remove("ToolCallId");
        data.Remove("tool_call_id");
        var id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToJsonString())));
        CliPermissionRequiredException pending;
        lock (_decisionLock)
        {
            if (_approvals.Overlaps(_denials))
                throw new ArgumentException("A permission cannot be both approved and denied.");
            if (_approvals.Remove(id))
                return Task.FromResult(true);
            if (_denials.Remove(id))
                return Task.FromResult(false);
            pending = Pending ??= new(new(id, MetadataGenerator.DescribePermissionRequest(request, invocation)));
        }
        cancellation.Cancel();
        return Task.FromException<bool>(pending);
    }
}
