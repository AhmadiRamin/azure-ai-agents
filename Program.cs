using Azure.AI.Agents.Persistent;
using AzureAIAgents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.ComponentModel.DataAnnotations;

var builder = Host.CreateApplicationBuilder(args);

var configuration = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.AddEnvironmentVariables()
	.Build();

builder.Services.AddSingleton<IConfiguration>(configuration);

// Register authentication services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IUserAuthenticationService, DeviceCodeAuthenticationService>();

// Register both agent services
builder.Services.AddSingleton<AgentService>();
builder.Services.AddSingleton<DelegatedAgentService>();

// Register agents from configuration
var agentsConfig = new AgentsConfiguration();
configuration.GetSection("Agents").Bind(agentsConfig.Agents);

if (agentsConfig.Agents.Count == 0)
{
	Console.WriteLine("No agents configured. Please check your appsettings.json file.");
	return;
}

// Register agents with both application and delegated versions
foreach (var agentConfig in agentsConfig.Agents)
{
	// Regular agent with application permissions
	builder.Services.AddKeyedSingleton<AzureAgent>(
		$"{agentConfig.Name}:Application",
		(provider, key) => new AzureAgent(
			provider.GetRequiredService<ILogger<AzureAgent>>(),
			provider.GetRequiredService<AgentService>(),
			agentConfig
		)
	);

	// Delegated agent with user permissions
	builder.Services.AddKeyedSingleton<DelegatedAzureAgent>(
		$"{agentConfig.Name}:Delegated",
		(provider, key) => new DelegatedAzureAgent(
			provider.GetRequiredService<ILogger<DelegatedAzureAgent>>(),
			provider.GetRequiredService<DelegatedAgentService>(),
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

try
{
	Console.WriteLine("🚀 Azure AI Agent with Permission Options");
	Console.WriteLine("=========================================");

	// Let user choose permission type
	Console.WriteLine("\nSelect permission type:");
	Console.WriteLine("1. Application Permissions (Service Account - your current setup)");
	Console.WriteLine("2. Delegated Permissions (User Authentication)");
	Console.Write("Choice (1-2): ");

	var permissionChoice = Console.ReadLine();
	bool useDelegatedPermissions = permissionChoice == "2";

	if (useDelegatedPermissions)
	{
		Console.WriteLine("\n🔐 Delegated Permissions Selected");
		Console.WriteLine("You will be prompted to authenticate...");
	}
	else
	{
		Console.WriteLine("\n🔧 Application Permissions Selected");
		Console.WriteLine("Using service account authentication...");
	}

	// Get available agents from configuration
	var availableAgents = new AgentsConfiguration();
	configuration.GetSection("Agents").Bind(availableAgents.Agents);

	// Let user select which agent to use
	Console.WriteLine($"\nAvailable agents:");
	for (int i = 0; i < availableAgents.Agents.Count; i++)
	{
		var agent = availableAgents.Agents[i];
		Console.WriteLine($"{i + 1}. {agent.Name}");
	}

	Console.Write($"Select an agent (1-{availableAgents.Agents.Count}): ");
	var selection = Console.ReadLine();

	if (!int.TryParse(selection, out int agentIndex) ||
		agentIndex < 1 || agentIndex > availableAgents.Agents.Count)
	{
		logger.LogError("Invalid agent selection");
		return;
	}

	var selectedAgentConfig = availableAgents.Agents[agentIndex - 1];

	if (useDelegatedPermissions)
	{
		// Use delegated permissions
		var agentKey = $"{selectedAgentConfig.Name}:Delegated";
		var selectedAgent = host.Services.GetRequiredKeyedService<DelegatedAzureAgent>(agentKey);

		logger.LogInformation("Initializing {AgentName} with delegated permissions...", selectedAgentConfig.Name);
		await selectedAgent.InitializeAsync();

		await RunChatLoop(selectedAgent, logger);
	}
	else
	{
		// Use application permissions (your existing setup)
		var agentKey = $"{selectedAgentConfig.Name}:Application";
		var selectedAgent = host.Services.GetRequiredKeyedService<AzureAgent>(agentKey);

		logger.LogInformation("Initializing {AgentName} with application permissions...", selectedAgentConfig.Name);
		await selectedAgent.InitializeAsync();

		await RunChatLoop(selectedAgent, logger);
	}
}
catch (Exception ex)
{
	logger.LogError(ex, "An error occurred: {Message}", ex.Message);
	Console.WriteLine($"❌ Error: {ex.Message}");
}

logger.LogInformation("Application completed");
await host.StopAsync();

// Helper method for chat loop (works with both agent types)
static async Task RunChatLoop<T>(T agent, ILogger logger) where T : class
{
	Console.WriteLine("\n💬 Chat with your agent (type 'exit' to quit):");

	if (agent is DelegatedAzureAgent)
	{
		Console.WriteLine("ℹ️  Using delegated permissions - agent will only access content you have permission to view.");
	}
	else
	{
		Console.WriteLine("ℹ️  Using application permissions - agent has broad access to configured SharePoint sites.");
	}

	Console.WriteLine();

	while (true)
	{
		Console.Write("You: ");
		var input = Console.ReadLine();

		if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
			break;

		logger.LogInformation("Processing query: {Query}", input);

		Console.Write("Assistant: ");

		// Handle both AzureAgent and DelegatedAzureAgent
		AsyncCollectionResult<StreamingUpdate> updates;
		if (agent is DelegatedAzureAgent delegatedAgent)
		{
			updates = await delegatedAgent.ProcessQueryAsync(input);
		}
		else if (agent is AzureAgent regularAgent)
		{
			updates = await regularAgent.ProcessQueryAsync(input);
		}
		else
		{
			throw new InvalidOperationException("Unknown agent type");
		}

		await foreach (var update in updates)
		{
			try
			{
				if (update.UpdateKind == StreamingUpdateReason.MessageUpdated)
				{
					if (update is MessageContentUpdate messageContent)
					{
						var text = messageContent.Text;
						if (!string.IsNullOrEmpty(text))
						{
							Console.Write(text);
						}

						if (messageContent.TextAnnotation != null)
						{
							Console.Write(messageContent.TextAnnotation.Url);
						}
					}
				}
			}
			catch (Exception updateEx)
			{
				logger.LogWarning(updateEx, "Error processing streaming update");
			}
		}

		// Display citations for both agent types
		if (agent is DelegatedAzureAgent delegatedAgentForCitations)
		{
			await delegatedAgentForCitations.DisplayCitationsAsync();
		}
		else if (agent is AzureAgent regularAgentForCitations)
		{
			await regularAgentForCitations.DisplayCitationsAsync();
		}

		Console.WriteLine();
	}
}