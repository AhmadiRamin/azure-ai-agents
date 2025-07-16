using Azure.AI.Agents.Persistent;
using BingGroundingAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

builder.Services.AddSingleton<IConfiguration>(configuration);
builder.Services.AddSingleton<AgentService>();

// Register agents from configuration
var agentsConfig = new AgentsConfiguration();
configuration.GetSection("Agents").Bind(agentsConfig.Agents);

// Register each agent as a named service
foreach (var agentConfig in agentsConfig.Agents)
{
    var agentType = agentConfig.Name; // Fallback to name if type is not set
    builder.Services.AddKeyedSingleton<AzureAgent>(
        agentType,
        (provider, key) =>
            new AzureAgent(
                provider.GetRequiredService<ILogger<AzureAgent>>(),
                provider.GetRequiredService<AgentService>(),
                agentConfig
            )
    );
}

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

// Get available agents from configuration
var availableAgents = new AgentsConfiguration();
configuration.GetSection("Agents").Bind(availableAgents.Agents);

// Let user select which agent to use
Console.WriteLine("Available agents:");
for (int i = 0; i < availableAgents.Agents.Count; i++)
{
    var agent = availableAgents.Agents[i];
    Console.WriteLine($"{i + 1}. {agent.Name}");
}

Console.Write("Select an agent (1-{0}): ", availableAgents.Agents.Count);
var selection = Console.ReadLine();

if (
    !int.TryParse(selection, out int agentIndex)
    || agentIndex < 1
    || agentIndex > availableAgents.Agents.Count
)
{
    logger.LogError("Invalid agent selection");
    return;
}

var selectedAgentConfig = availableAgents.Agents[agentIndex - 1];
var agentKey = selectedAgentConfig.Name;
var selectedAgent = host.Services.GetRequiredKeyedService<AzureAgent>(agentKey);

try
{
    logger.LogInformation("Initializing {AgentName}...", selectedAgentConfig.Name);
    await selectedAgent.InitializeAsync();

    while (true)
    {
        Console.Write("\nYou: ");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
            break;

        logger.LogInformation("Processing query: {Query}", input);

        Console.Write("Assistant: ");
        await foreach (var update in await selectedAgent.ProcessQueryAsync(input))
        {
            try
            {
                if (update.UpdateKind == StreamingUpdateReason.MessageUpdated)
                {
                    if (update is MessageContentUpdate messageContent)
                    {
                        // Filter out citation markers and special characters
                        var text = messageContent.Text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            Console.Write(text);
                        }
                    }
                }
            }
            catch (Exception updateEx)
            {
                logger.LogWarning(updateEx, "Error processing streaming update");
            }
        }

        // Output references (citations)
        await selectedAgent.GetCitationSourcesAsync();
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred: {Message}", ex.Message);
}

logger.LogInformation("Application completed");
await host.StopAsync();
