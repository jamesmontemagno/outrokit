using PodcastMetadataGenerator.Console.Cli;
using PodcastMetadataGenerator.Console.UI;
using PodcastMetadataGenerator.Core.Services;

if (CliApplication.ShouldRun(args))
{
    return await new CliApplication().RunAsync(args, System.Console.Out, System.Console.Error);
}

// Check Copilot status
var copilotAuth = new CopilotAuthService();
var copilotStatus = await copilotAuth.CheckStatusAsync();

// Run main workflow (will show header with status)
var workflow = new AppWorkflow();
await workflow.RunAsync(args, copilotStatus);

return 0;
