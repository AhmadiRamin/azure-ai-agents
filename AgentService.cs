using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using AzureAIAgents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.ComponentModel.DataAnnotations;
using ToolDefinition = Azure.AI.Agents.Persistent.ToolDefinition;
using ToolResources = Azure.AI.Agents.Persistent.ToolResources;

namespace AzureAIAgents
{
	public class AgentService
	{
		private readonly IConfiguration _configuration;
		private readonly ILogger<AgentService> _logger;
		private readonly PersistentAgentsClient _persistentClient;
		private readonly Connections _connections;
		private readonly AIProjectClient _projectClient;
		private bool _disposed = false;

		public AgentService(IConfiguration configuration, ILogger<AgentService> logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

			try
			{
				var connectionString = _configuration["AzureAI:ConnectionString"]
					?? throw new InvalidOperationException("Azure AI connection string is not configured");

				// Create Azure AI Project client with proper authentication
				_projectClient = new AIProjectClient(
					new Uri(connectionString),
					new DefaultAzureCredential()
				);
				_connections = _projectClient.GetConnectionsClient();

				// Create Azure AI persistent client
				_persistentClient = new PersistentAgentsClient(
					connectionString,
					new DefaultAzureCredential()
				);

				_logger.LogInformation("Successfully initialized Azure AI clients");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to initialize Azure AI clients");
				throw;
			}
		}

		public async Task<PersistentAgent?> GetAgentAsync(string name)
		{
			try
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

				var agents = _persistentClient.Administration.GetAgentsAsync();
				var agent = await agents.Where(a => a.Name == name).FirstOrDefaultAsync();

				if (agent == null)
				{
					_logger.LogWarning("Agent '{AgentName}' not found", name);
				}
				else
				{
					_logger.LogDebug("Found existing agent '{AgentName}' with ID: {AgentId}", name, agent.Id);
				}

				return agent;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to get agent {AgentName}", name);
				return null;
			}
		}

		public async Task<Connection?> GetConnectionByNameAsync(string connectionName)
		{
			try
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));

				await foreach (var connection in _connections.GetConnectionsAsync())
				{
					if (string.Equals(connection.Name, connectionName, StringComparison.OrdinalIgnoreCase))
					{
						_logger.LogDebug("Found connection '{ConnectionName}' with ID: {ConnectionId}",
							connection.Name, connection.Id);
						return connection;
					}
				}

				_logger.LogWarning("Connection '{ConnectionName}' not found", connectionName);
				return null;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to get connection '{ConnectionName}'", connectionName);
				throw;
			}
		}

		public async Task<PersistentAgent> CreateAgentAsync(AgentOptions agentOptions)
		{
			try
			{
				ArgumentNullException.ThrowIfNull(agentOptions, nameof(agentOptions));

				// Validate configuration
				var validationResults = new List<ValidationResult>();
				var validationContext = new ValidationContext(agentOptions);
				if (!Validator.TryValidateObject(agentOptions, validationContext, validationResults, true))
				{
					var errors = string.Join(", ", validationResults.Select(vr => vr.ErrorMessage));
					throw new ArgumentException($"Invalid agent configuration: {errors}");
				}

				// Check if agent already exists
				var existingAgent = await GetAgentAsync(agentOptions.Name);
				if (existingAgent != null)
				{
					_logger.LogInformation("Agent '{AgentName}' already exists with ID: {AgentId}",
						agentOptions.Name, existingAgent.Id);
					return existingAgent;
				}

				// Create tools
				var toolDefinitions = new List<ToolDefinition>();

				if (agentOptions.Tools != null)
				{
					foreach (var tool in agentOptions.Tools)
					{
						var toolDefinition = await CreateToolDefinitionAsync(tool);
						if (toolDefinition != null)
						{
							toolDefinitions.Add(toolDefinition);
						}
					}
				}

				// Create agent
				var agent = await _persistentClient.Administration.CreateAgentAsync(
					model: agentOptions.Deployment,
					name: agentOptions.Name,
					description: agentOptions.Description,
					instructions: agentOptions.Instructions,
					tools: toolDefinitions,
					toolResources: new ToolResources()
				);

				_logger.LogInformation("Successfully created agent '{AgentName}' with ID: {AgentId} and {ToolCount} tools",
					agentOptions.Name, agent.Value.Id, toolDefinitions.Count);

				return agent;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create agent '{AgentName}'", agentOptions?.Name ?? "Unknown");
				throw;
			}
		}

		private async Task<ToolDefinition?> CreateToolDefinitionAsync(ToolsOptions tool)
		{
			try
			{
				return tool.ToolType switch
				{
					"SharePointGrounding" => await CreateSharePointToolAsync(tool),
					"BingGroundingSearch" => await CreateBingSearchToolAsync(tool),
					"CustomBingGroundingSearch" => await CreateCustomBingSearchToolAsync(tool),
					_ => throw new NotSupportedException($"Tool type '{tool.ToolType}' is not supported")
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create tool definition for {ToolType} with connection {ConnectionName}",
					tool.ToolType, tool.ConnectionName);
				return null;
			}
		}

		private async Task<ToolDefinition> CreateSharePointToolAsync(ToolsOptions tool)
		{
			var connection = await GetConnectionByNameAsync(tool.ConnectionName);
			if (connection?.Id == null)
			{
				throw new InvalidOperationException($"SharePoint connection '{tool.ConnectionName}' not found or invalid");
			}

			_logger.LogInformation("Creating SharePoint grounding tool with connection: {ConnectionName}",
				tool.ConnectionName);
			
			return new SharepointToolDefinition(
				new SharepointGroundingToolParameters(connection.Id)
			);
		}

		private async Task<ToolDefinition> CreateBingSearchToolAsync(ToolsOptions tool)
		{
			var connection = await GetConnectionByNameAsync(tool.ConnectionName);
			if (connection?.Id == null)
			{
				throw new InvalidOperationException($"Bing Search connection '{tool.ConnectionName}' not found or invalid");
			}

			return new BingGroundingToolDefinition(
				new BingGroundingSearchToolParameters(
					[new BingGroundingSearchConfiguration(connection.Id)]
				)
			);
		}

		private async Task<ToolDefinition> CreateCustomBingSearchToolAsync(ToolsOptions tool)
		{
			var connection = await GetConnectionByNameAsync(tool.ConnectionName);
			if (connection?.Id == null)
			{
				throw new InvalidOperationException($"Custom Bing Search connection '{tool.ConnectionName}' not found or invalid");
			}

			var configuration = new BingCustomSearchConfiguration(
				connection.Id,
				tool.ConfigurationName ?? "default")
			{
				Count = 5,
				SetLang = "en",
				Market = "en-us"
			};

			return new BingCustomSearchToolDefinition(
				new BingCustomSearchToolParameters([configuration])
			);
		}

		public async Task<PersistentAgentThread> CreateThreadAsync()
		{
			try
			{
				var thread = await _persistentClient.Threads.CreateThreadAsync();
				_logger.LogDebug("Created new thread with ID: {ThreadId}", thread.Value.Id);
				return thread;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create thread");
				throw;
			}
		}

		public async Task<PersistentThreadMessage> CreateMessageAsync(string threadId, string query)
		{
			try
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(threadId, nameof(threadId));
				ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

				var message = await _persistentClient.Messages.CreateMessageAsync(
					threadId,
					MessageRole.User,
					query
				);

				_logger.LogDebug("Created message in thread {ThreadId}", threadId);
				return message;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create message in thread {ThreadId}", threadId);
				throw;
			}
		}

		public AsyncCollectionResult<StreamingUpdate> CreateStreamingAsync(string threadId, string agentId)
		{
			try
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(threadId, nameof(threadId));
				ArgumentException.ThrowIfNullOrWhiteSpace(agentId, nameof(agentId));

				_logger.LogDebug("Starting streaming for thread {ThreadId} with agent {AgentId}", threadId, agentId);
				return _persistentClient.Runs.CreateRunStreamingAsync(threadId, agentId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create streaming for thread {ThreadId}, agent {AgentId}",
					threadId, agentId);
				throw;
			}
		}

		public async Task<List<MessageTextUriCitationAnnotation>> GetCitationsAsync(string threadId)
		{
			var citations = new List<MessageTextUriCitationAnnotation>();

			try
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(threadId, nameof(threadId));

				var messages = _persistentClient.Messages.GetMessagesAsync(threadId);
				var messagesList = await messages.ToListAsync();

				if (messagesList.Count > 0)
				{
					var lastMessage = messagesList[0]; // Messages are returned in reverse chronological order
					foreach (var contentItem in lastMessage.ContentItems)
					{
						if (contentItem is MessageTextContent textItem)
						{
							foreach (var annotation in textItem.Annotations)
							{
								if (annotation is MessageTextUriCitationAnnotation uriCitation)
								{
									citations.Add(uriCitation);
								}
							}
						}
					}
				}

				_logger.LogDebug("Retrieved {CitationCount} citations from thread {ThreadId}", citations.Count, threadId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to get citations for thread {ThreadId}", threadId);
			}

			return citations;
		}

		public async Task<bool> ValidateConnectionAsync(string connectionName)
		{
			try
			{
				var connection = await GetConnectionByNameAsync(connectionName);
				var isValid = connection != null;

				_logger.LogInformation("Connection validation for '{ConnectionName}': {IsValid}",
					connectionName, isValid ? "Valid" : "Invalid");

				return isValid;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to validate connection: {ConnectionName}", connectionName);
				return false;
			}
		}

	}
}