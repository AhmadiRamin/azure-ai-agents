using System.ClientModel;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace BingGroundingAgent;

public sealed class BingGroundingAgent
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BingGroundingAgent> _logger;
    private PersistentAgent? _agent;
    private PersistentAgentsClient? _persistentClient;
    private Connections? _connections;
    private AIProjectClient? _projectClient;

    public BingGroundingAgent(IConfiguration configuration, ILogger<BingGroundingAgent> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Validate configuration
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
            var connectionName =
                _configuration["BingGrounding:ConnectionName"]
                ?? throw new InvalidOperationException(
                    "Bing grounding connection name is not configured"
                );

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

            // Create kernel with Azure OpenAI configuration
            var kernel = Kernel
                .CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: deploymentName,
                    endpoint: endpoint,
                    apiKey: apiKey
                )
                .Build();

            // Define the agent with Bing Search grounding
            var bingConnection = await GetConnectionByToolResourceNameAsync(connectionName);
            if (bingConnection == null)
            {
                throw new InvalidOperationException(
                    $"Failed to get Bing Search connection '{connectionName}'"
                );
            }

            var bingGroundingTool = new BingGroundingToolDefinition(
                new BingGroundingSearchToolParameters(
                    [new BingGroundingSearchConfiguration(bingConnection.Id)]
                )
            );
            var agentName = "Bing Grounding Agent";
            var agent = await this.GetAgentAsync(agentName);
            if (agent == null)
            {
                _agent = await _persistentClient.Administration.CreateAgentAsync(
                    model: deploymentName,
                    name: agentName,
                    instructions: """
                    You are a helpful assistant that can search the web for current information.
                    When users ask questions that require up-to-date information, use the Bing search tool
                    to find relevant information and provide accurate, grounded responses.
                    Always cite your sources when providing information from search results.
                    """,
                    tools: [bingGroundingTool]
                );
            }
            _logger.LogInformation(
                "Agent initialized successfully with Bing Search grounding. Agent ID: {AgentId}",
                _agent.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Bing Grounding Agent");
            throw;
        }
    }

    public async Task<AsyncCollectionResult<StreamingUpdate>> ProcessQueryAsync(string userQuery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userQuery);

        if (_persistentClient == null || _agent == null)
        {
            throw new InvalidOperationException(
                "Agent must be initialized before processing queries. Call InitializeAsync() first."
            );
        }

        try
        {
            _logger.LogInformation("Processing query: {Query}", userQuery);

            // Create a new thread for the conversation
            var thread = await _persistentClient.Threads.CreateThreadAsync();

            // Add user message
            await _persistentClient.Messages.CreateMessageAsync(
                thread.Value.Id,
                MessageRole.User,
                userQuery
            );

            // Get response from agent
            return _persistentClient.Runs.CreateRunStreamingAsync(thread.Value.Id, _agent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", userQuery);
            throw;
        }
    }

    private async Task<PersistentAgent?> GetAgentAsync(string name)
    {
        try
        {
            var agent = await _persistentClient.Administration.GetAgentAsync(name);
            return agent;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to get agent {AgentName}", name);
            return null;
        }
    }

    private async Task<Connection?> GetConnectionByToolResourceNameAsync(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (_connections == null)
        {
            throw new InvalidOperationException("Connections client is not initialized");
        }

        await foreach (
            var connection in _connections.GetConnectionsAsync(
                connectionType: ConnectionType.APIKey
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
}
