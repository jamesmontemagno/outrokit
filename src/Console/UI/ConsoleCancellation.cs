namespace PodcastMetadataGenerator.Console.UI;

/// <summary>
/// Lets Ctrl+C cancel one long-running operation instead of terminating the app.
/// </summary>
public static class ConsoleCancellation
{
    /// <summary>
    /// Runs <paramref name="operation"/> with a token that is cancelled when the user presses Ctrl+C.
    /// Ctrl+C returns to its default behavior once the operation completes.
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ctrl+C raced with the operation finishing; there is nothing left to cancel.
            }
        };

        System.Console.CancelKeyPress += handler;
        try
        {
            return await operation(cancellation.Token);
        }
        finally
        {
            System.Console.CancelKeyPress -= handler;
        }
    }
}
