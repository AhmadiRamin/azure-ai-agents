using System.ClientModel;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;

namespace BingGroundingAgent;

public sealed class AzureAgent
{
    private readonly ILogger _logger;
    private readonly AgentService _agentService;
    private readonly AgentOptions _agentOptions;
    private PersistentAgent? _agent;
    private PersistentAgentThread? _thread;

    public AzureAgent(ILogger logger, AgentService agentService, AgentOptions agentOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
        _agentOptions = agentOptions ?? throw new ArgumentNullException(nameof(agentOptions));
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Define the agent using the provided agent options
            _agent = await _agentService.CreateAgentAsync(_agentOptions);
            _logger.LogInformation(
                "{AgentName} initialized successfully. Agent ID: {AgentId}",
                _agentOptions.Name,
                _agent?.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize {AgentName}", _agentOptions.Name);
            throw;
        }
    }

    public async Task<AsyncCollectionResult<StreamingUpdate>> ProcessQueryAsync(string userQuery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userQuery);
        if (_agent == null)
        {
            throw new InvalidOperationException(
                "Agent is not initialized. Call InitializeAsync first."
            );
        }

        try
        {
            _logger.LogInformation("Processing query: {Query}", userQuery);

            // Create a new thread for the conversation
            var thread = await _agentService.CreateThreadAsync();
            _thread = thread;

            // Add user message
            await _agentService.CreateMessageAsync(thread.Id, userQuery);

            // Get response from agent
            return _agentService.CreateStreaming(thread.Id, _agent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", userQuery);
            throw;
        }
    }

    public async Task GetCitationSourcesAsync()
    {
        if (_agent == null)
        {
            throw new InvalidOperationException(
                "Agent is not initialized. Call InitializeAsync first."
            );
        }

        if (_thread == null)
        {
            throw new InvalidOperationException("No active thread. Process a query first.");
        }

        var citations = await _agentService.GetMessageUrlCitationsAsync(_agent.Id, _thread.Id);

        if (citations.Count > 0)
        {
            Console.WriteLine();
			Console.WriteLine("---------------------------------");
			Console.WriteLine("References:");

            foreach (var citation in citations)
            {
                Console.WriteLine($"* {citation.UriCitation.Uri}");
            }
        }
    }
}
