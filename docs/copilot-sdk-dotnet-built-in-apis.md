# Using GitHub Copilot SDK built-in APIs in .NET

This note is based on the `v1.0.16` release of [`github/copilot-sdk`](https://github.com/github/copilot-sdk) and its .NET package, `GitHub.Copilot.SDK`.

## Prerequisites

- .NET 8 or later
- A GitHub Copilot subscription and authentication, unless you are using BYOK provider settings

The .NET package bundles the compatible Copilot runtime. You can optionally install the Copilot CLI separately to authenticate interactively or set `COPILOT_CLI_PATH` to use a specific executable.

Install the SDK:

```bash
dotnet add package GitHub.Copilot.SDK
```

## Main built-in APIs

### `CopilotClient`

`CopilotClient` is the main entry point.

Common things you do with it:

- `StartAsync()` / `StopAsync()` - start or stop the CLI-backed server
- `CreateSessionAsync(...)` - create a new agent session
- `ResumeSessionAsync(sessionId, ...)` - reopen an existing session
- `ListSessionsAsync()` / `DeleteSessionAsync(sessionId)` - manage sessions
- `PingAsync()` - verify connectivity

Useful options in `CopilotClientOptions` include:

- `Connection` with `RuntimeConnection.ForStdio(path: ...)`, `ForTcp(...)`, or `ForUri(...)`
- `WorkingDirectory`
- `BaseDirectory`
- `GitHubToken`
- `Telemetry`
- `ClientInfo` to identify your application on the connection

### `SessionConfig`

`SessionConfig` controls how a session behaves.

The most useful built-in properties are:

- `ClientName` to identify your application on the session
- `Model`
- `ReasoningEffort`
- `Streaming`
- `SystemMessage`
- `AvailableTools`
- `ExcludedTools`
- `Tools` for custom tool handlers
- `Provider` for BYOK scenarios
- `InfiniteSessions`
- `OnUserInputRequest`
- `Hooks`

### `CopilotSession`

A `CopilotSession` represents one conversation.

Core APIs:

- `SendAsync(new MessageOptions { Prompt = ... })`
- `On(...)` to subscribe to streamed events
- `GetEventsAsync()`
- `AbortAsync()`

## Minimal example

```csharp
using GitHub.Copilot;

await using var client = new CopilotClient();

var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-5"
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

## Identifying your application

Name your app on both the client connection and each session. The connection-level name lets runtime telemetry be attributed to your app, and the session-level name is recorded with the session (for example as `client_name` in its workspace metadata).

```csharp
const string ClientName = "podcast-metadata-generator";

await using var client = new CopilotClient(new CopilotClientOptions
{
    ClientInfo = new CopilotClientInfo { ApplicationName = ClientName }
});

var session = await client.CreateSessionAsync(new SessionConfig
{
    ClientName = ClientName,
    Model = "gpt-5"
});
```

This app sets both from `CopilotClientFactory.ClientName`.

## Restricting or allowing built-in tools

By default, the SDK runs the Copilot CLI with first-party tools enabled. In .NET, you can limit that surface with `AvailableTools` or `ExcludedTools`.

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-5",
    AvailableTools = ["read_file", "list_directory"],
    ExcludedTools = ["run_in_terminal"]
});
```

Use this pattern when you want the agent to stay read-only or avoid shell access.

## Adding your own tool APIs

The .NET SDK can expose application-specific tools back to the agent. The upstream examples use `AIFunctionFactory.Create` from `Microsoft.Extensions.AI`.

```csharp
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using System.ComponentModel;

var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-5",
    Tools =
    [
        AIFunctionFactory.Create(
            ([Description("Podcast episode id")] string id) => GetEpisodeAsync(id),
            "get_episode",
            "Gets episode metadata from the local application")
    ]
});
```

This is the main way to connect Copilot to your own app APIs.

## User input and hooks

Two useful built-in extension points:

- `OnUserInputRequest` lets the agent ask the user a follow-up question through the `ask_user` flow.
- `Hooks` lets you intercept session events such as pre-tool use, post-tool use, session start, session end, and error handling.

## Streaming responses

Set `Streaming = true` in `SessionConfig` and subscribe to session events. The SDK emits incremental events before the final `AssistantMessageEvent`.

## Infinite sessions

The latest .NET SDK also supports `InfiniteSessions`, which enables automatic context compaction for long-running sessions. When enabled, the session exposes a workspace path that contains checkpoints and saved files.

## Practical guidance

For most .NET apps, the normal flow is:

1. Create a `CopilotClient`
2. Create a session with `CreateSessionAsync`
3. Set `Model`, `Streaming`, and tool restrictions in `SessionConfig`
4. Call `SendAsync` with a prompt
5. Listen to session events until `SessionIdleEvent`
6. Add custom tools only for the app-specific actions Copilot should perform

## Notes

- The SDK is generally available and follows semantic versioning.
- The .NET package bundles the compatible Copilot runtime.
- The upstream README notes that the SDK supports multiple auth approaches, including logged-in CLI auth, GitHub tokens, and BYOK provider configuration.
