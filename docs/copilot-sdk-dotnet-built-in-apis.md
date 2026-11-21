# Using Microsoft Copilot SDK built-in APIs in .NET

This note is based on the `v2.0.1` release of [`microsoft/copilot-sdk`](https://github.com/microsoft/copilot-sdk) and its .NET package, `Microsoft.Copilot.SDK`. Version 2 renamed the SDK from GitHub Copilot SDK, moved the namespaces from `GitHub.Copilot` to `Microsoft.Copilot`, and raised the SDK protocol to version 4. See the upstream [v2 breaking changes](https://github.com/microsoft/copilot-sdk/blob/main/docs/v2-breaking-changes.md) guide when migrating from v1.

## Prerequisites

- .NET 8 or later
- A GitHub Copilot subscription and authentication, unless you are using BYOK provider settings

By default the .NET package bundles a compatible Copilot runtime and loads it into your application's process. You can optionally install the Copilot CLI separately to authenticate interactively, or run it as a child process instead.

OutroKit opts out of the bundled runtime with `<CopilotSkipCliDownload>true</CopilotSkipCliDownload>` in `src/Core`, which keeps its package small. It always starts the installed Copilot CLI as a child process with `RuntimeConnection.ForSpawnedStdio(path: ...)`, so that CLI must be recent enough to speak protocol version 4.

Install the SDK:

```bash
dotnet add package Microsoft.Copilot.SDK
```

## Main built-in APIs

### `CopilotClient`

`CopilotClient` is the main entry point.

Common things you do with it:

- `StartAsync()` / `StopAsync()` - start or stop the runtime connection
- `CreateSessionAsync(...)` - create a new agent session
- `ResumeSessionAsync(sessionId, ...)` - reopen an existing session
- `ListSessionsAsync()` / `DeleteSessionAsync(sessionId)` - manage sessions
- `ListModelsAsync()` - list the models available to the signed-in user
- `GetAuthStatusAsync()` - check whether the user is signed in
- `PingAsync()` - verify connectivity

`CopilotClientOptions.Mode` is required. `CopilotClientMode.CodingAgent` matches the v1 default behavior. Other useful options include:

- `Runtime` with `RuntimeConnection.ForInProcess(...)`, `ForSpawnedStdio(path: ...)`, `ForSpawnedTcp(...)`, `ForTcp(host, port)`, or `ForDefault(...)`. Process settings such as the working directory, environment, and base directory belong on spawned connections
- `GitHub.Token` and `GitHub.UseLoggedInUser`
- `Observability.LogLevel` and `Observability.Telemetry`
- `ClientInfo` to identify your application on the connection

### `CreateSessionOptions`

`CreateSessionOptions` (formerly `SessionConfig`) controls how a session behaves. `ClientName`, `Model`, `SystemMessage`, and `WorkingDirectory` stay at the root, and related settings are grouped:

- `Inference.ReasoningEffort`
- `Events.Streaming`
- `Tools.AvailableTools`, `Tools.ExcludedTools`, and `Tools.Definitions` for custom tool handlers
- `Permissions.OnRequest` to approve or deny tool permission requests
- `Interaction.OnUserInputRequest`
- `Hooks.Handlers`
- `State.InfiniteSessions`
- `Inference.Providers` and `Inference.Models` for BYOK scenarios

### `CopilotSession`

A `CopilotSession` represents one conversation.

Core APIs:

- `SendAsync(new MessageOptions { Prompt = ... })`
- `On(...)` to subscribe to streamed events
- `GetEventsAsync()`
- `AbortAsync()`

## Minimal example

```csharp
using Microsoft.Copilot;

await using var client = new CopilotClient(new CopilotClientOptions
{
    Mode = CopilotClientMode.CodingAgent
});

var session = await client.CreateSessionAsync(new CreateSessionOptions
{
    Model = "gpt-5",
    Permissions = new() { OnRequest = PermissionHandler.ApproveAll }
});

var finished = new TaskCompletionSource();

using var subscription = session.On<SessionEvent>(evt =>
{
    if (evt is AssistantMessageEvent message)
    {
        Console.WriteLine(message.Data.Content);
    }
    else if (evt is SessionIdleEvent)
    {
        finished.TrySetResult();
    }
});

await session.SendAsync(new MessageOptions
{
    Prompt = "Summarize this repository in 3 bullets."
});

await finished.Task;
```

`PermissionHandler.ApproveAll` keeps the example short. In a real app, ask the user and return their decision, as OutroKit does. If you leave `Permissions.OnRequest` unset, permission requests stay pending rather than being denied.

## Running the installed Copilot CLI

To run a Copilot CLI you installed instead of the in-process runtime, use a spawned stdio connection. This is what OutroKit's `CopilotClientFactory.CreateClient()` does:

```csharp
await using var client = new CopilotClient(new CopilotClientOptions
{
    Mode = CopilotClientMode.CodingAgent,
    Runtime = RuntimeConnection.ForSpawnedStdio(path: "/opt/homebrew/bin/copilot")
});
```

Setting the `COPILOT_SDK_DEFAULT_CONNECTION=stdio` environment variable has the same effect when no `Runtime` is configured.

If the CLI and SDK speak different protocol versions, `StartAsync` fails with an "SDK protocol version mismatch" error. Update whichever side is older.

## Identifying your application

Name your app on both the client connection and each session. The connection-level name lets runtime telemetry be attributed to your app, and the session-level name is recorded with the session (for example as `client_name` in its workspace metadata).

```csharp
const string ClientName = "outrokit";

await using var client = new CopilotClient(new CopilotClientOptions
{
    Mode = CopilotClientMode.CodingAgent,
    ClientInfo = new CopilotClientInfo { ApplicationName = ClientName }
});

var session = await client.CreateSessionAsync(new CreateSessionOptions
{
    ClientName = ClientName,
    Model = "gpt-5",
    Permissions = new() { OnRequest = PermissionHandler.ApproveAll }
});
```

This app sets both from `CopilotClientFactory.ClientName`.

## Restricting or allowing built-in tools

By default, the runtime enables its first-party tools. In .NET, you can limit that surface with `Tools.AvailableTools` or `Tools.ExcludedTools`.

```csharp
var session = await client.CreateSessionAsync(new CreateSessionOptions
{
    Model = "gpt-5",
    Permissions = new() { OnRequest = PermissionHandler.ApproveAll },
    Tools = new()
    {
        AvailableTools = ["read_file", "list_directory"],
        ExcludedTools = ["run_in_terminal"]
    }
});
```

Use this pattern when you want the agent to stay read-only or avoid shell access. OutroKit sets `AvailableTools = []` so metadata sessions only use the transcript it sends.

## Adding your own tool APIs

The .NET SDK can expose application-specific tools back to the agent. The upstream examples use `AIFunctionFactory.Create` from `Microsoft.Extensions.AI`.

```csharp
using Microsoft.Copilot;
using Microsoft.Extensions.AI;
using System.ComponentModel;

var session = await client.CreateSessionAsync(new CreateSessionOptions
{
    Model = "gpt-5",
    Permissions = new() { OnRequest = PermissionHandler.ApproveAll },
    Tools = new()
    {
        Definitions =
        [
            AIFunctionFactory.Create(
                ([Description("Podcast episode id")] string id) => GetEpisodeAsync(id),
                "get_episode",
                "Gets episode metadata from the local application")
        ]
    }
});
```

This is the main way to connect Copilot to your own app APIs.

## User input and hooks

Two useful built-in extension points:

- `Interaction.OnUserInputRequest` lets the agent ask the user a follow-up question through the `ask_user` flow.
- `Hooks.Handlers` lets you intercept session events such as pre-tool use, post-tool use, session start, session end, and error handling.

## Streaming responses

Set `Events = new() { Streaming = true }` in `CreateSessionOptions` and subscribe to session events. The SDK emits incremental events before the final `AssistantMessageEvent`.

## Infinite sessions

Set `State.InfiniteSessions` to enable automatic context compaction for long-running sessions. When enabled, the session exposes a workspace path that contains checkpoints and saved files.

## Practical guidance

For most .NET apps, the normal flow is:

1. Create a `CopilotClient` with an explicit `Mode`
2. Create a session with `CreateSessionAsync`
3. Set `Model`, `Events.Streaming`, `Permissions.OnRequest`, and tool restrictions in `CreateSessionOptions`
4. Call `SendAsync` with a prompt
5. Listen to session events until `SessionIdleEvent`
6. Add custom tools only for the app-specific actions Copilot should perform

## Notes

- The SDK is generally available and follows semantic versioning.
- Version 2 hosts the bundled runtime in your process by default. In-process hosting is not a sandbox, and a native runtime crash can end your application. Use a spawned connection when you need process isolation.
- Experimental APIs report the `MCPSDK001` diagnostic (formerly `GHCP001`).
- The upstream README notes that the SDK supports multiple auth approaches, including logged-in CLI auth, GitHub tokens, and BYOK provider configuration.
