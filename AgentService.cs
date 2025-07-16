using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using ToolDefinition = Azure.AI.Agents.Persistent.ToolDefinition;
using ToolResources = Azure.AI.Agents.Persistent.ToolResources;

namespace BingGroundingAgent
{
    public class AgentService
    {
		private readonly IConfiguration _configuration;
		private readonly ILogger<AgentService> _logger;
		private PersistentAgentsClient? _persistentClient;
		private Connections? _connections;
		private AIProjectClient? _projectClient;
		public AgentService(IConfiguration configuration, ILogger<AgentService> logger)
        {
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			// Validate configuration
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			var connectionString =
				_configuration["AzureAI:ConnectionString"]
				?? throw new InvalidOperationException(
					"Azure AI connection string is not configured"
				);
			var deploymentName =
				_configuration["AzureOpenAI:DeploymentName"]
				?? throw new InvalidOperationException(
					"Azure OpenAI deployment name is not configured"
				);
			var endpoint =
				_configuration["AzureOpenAI:Endpoint"]
				?? throw new InvalidOperationException("Azure OpenAI endpoint is not configured");
			var apiKey =
				_configuration["AzureOpenAI:ApiKey"]
				?? throw new InvalidOperationException("Azure OpenAI API key is not configured");
			// Create Azure AI Project client
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
		}

		public async Task<PersistentAgent?> GetAgentAsync(string name)
		{
			try
			{
				var agents = _persistentClient.Administration.GetAgentsAsync();
				var agent = await agents.Where(a => a.Name == name).FirstOrDefaultAsync();
				return agent;
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Failed to get agent {AgentName}", name);
				return null;
			}
		}

		public async Task<Connection?> GetConnectionByToolResourceNameAsync(string toolName)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

			if (_connections == null)
			{
				throw new InvalidOperationException("Connections client is not initialized");
			}

			await foreach (
				var connection in _connections.GetConnectionsAsync(
				)
			)
			{
				if (string.Equals(connection.Name, toolName, StringComparison.OrdinalIgnoreCase))
				{
					return connection;
				}
			}
			_logger.LogWarning("Connection with name '{ConnectionName}' not found", toolName);
			return null;
		}

		public async Task<PersistentAgent?> CreateAgentAsync(AgentOptions agentOptions)
		{
			var agent = await this.GetAgentAsync(agentOptions.Name);

			if (agent == null)
			{
				// Setup tools internally
				List<ToolsOptions> toolList = agentOptions.Tools;
				List<ToolDefinition> toolDefinitionList = new();

				foreach (ToolsOptions tool in toolList)
				{
					if (tool.ToolType == "BingGroundingSearch")
					{
						var bingConnection = await this.GetConnectionByToolResourceNameAsync(
							tool.ConnectionName
						);

						var bingGroundingTool = new BingGroundingToolDefinition(
							new BingGroundingSearchToolParameters(
								[new BingGroundingSearchConfiguration(bingConnection.Id)]
							)
						);

						if (bingConnection?.Id is null)
						{
							throw new InvalidOperationException("Failed to get Bing Search connection");
						}
						toolDefinitionList.Add(bingGroundingTool);
					}
					else if (tool.ToolType == "CustomBingGroundingSearch")
					{
						var connection = await this.GetConnectionByToolResourceNameAsync(
							tool.ConnectionName
						);

						var customConfiguration = new BingCustomSearchConfiguration(connection.Id, tool.ConfigurationName)
						{
							Count = 5,              // Set the number of results  
							SetLang = "en",         // Set the language  
							Market = "en-us"        // Set the market  
						};


						// Wrap the configuration into an IEnumerable  
						IEnumerable<BingCustomSearchConfiguration> configs = new List<BingCustomSearchConfiguration> { customConfiguration };

						BingCustomSearchToolDefinition bingCustomSearchTool = new(
							 new BingCustomSearchToolParameters(configs) 
						);
						toolDefinitionList.Add(bingCustomSearchTool);
					}
				}

				ToolResources toolResources = new();

				if (toolDefinitionList.Count > 0)
				{
					agent = await _persistentClient.Administration.CreateAgentAsync(
						agentOptions.Deployment,
						agentOptions.Name,
						agentOptions.Description,
						agentOptions.Instructions,
						tools: toolDefinitionList,
						toolResources: toolResources
					);
				}
				else
				{
					agent = await _persistentClient.Administration.CreateAgentAsync(
						agentOptions.Deployment,
						agentOptions.Name,
						agentOptions.Description,
						agentOptions.Instructions
					);
				}
			}

			return agent;
		}

		public async Task<PersistentAgentThread> CreateThreadAsync()
		{
			return await _persistentClient.Threads.CreateThreadAsync();
		}
		public async Task<PersistentThreadMessage> CreateMessageAsync(string threadId, string query)
		{
			return await _persistentClient.Messages.CreateMessageAsync(threadId, Azure.AI.Agents.Persistent.MessageRole.User, query);
		}
		public AsyncCollectionResult<Azure.AI.Agents.Persistent.StreamingUpdate> CreateStreaming(string threadId, string agentId)
		{
			return _persistentClient.Runs.CreateRunStreamingAsync(threadId, agentId);			
		}
		public async Task<List<MessageTextUriCitationAnnotation>> GetMessageUrlCitationsAsync(string agentId,string threadId)
		{
			List<MessageTextUriCitationAnnotation> citations = new();
			var afterRunMessagesResponse = _persistentClient.Messages.GetMessagesAsync(threadId);
			var messages = await afterRunMessagesResponse.ToListAsync();
			PersistentThreadMessage lastMessage = messages[0];

			foreach (var contentItem in lastMessage.ContentItems)
			{
				if (contentItem is MessageTextContent textItem)
				{
					foreach (var citation in textItem.Annotations)
					{
						MessageTextUriCitationAnnotation urlCitation =
							(MessageTextUriCitationAnnotation)citation;
						citations.Add(urlCitation);
					}
				}
			}

			return citations;
		}
	}
}
