using System.ClientModel;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;

namespace AzureAIAgents;

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

            var thread = await _agentService.CreateThreadAsync();
            _thread = thread;

            await _agentService.CreateMessageAsync(thread.Id, userQuery);

            return _agentService.CreateStreamingAsync(thread.Id, _agent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", userQuery);
            throw;
        }
    }

	public async Task DisplayCitationsAsync()
	{
		if (_thread == null)
		{
			_logger.LogWarning("No active thread to get citations from");
			return;
		}

		try
		{
			var citations = await _agentService.GetCitationsAsync(_thread.Id);

			if (citations.Count > 0)
			{
				Console.WriteLine();
				Console.WriteLine("Sources:");
				Console.WriteLine(new string('-', 50));

				for (int i = 0; i < citations.Count; i++)
				{
					var citation = citations[i];
					Console.WriteLine($"{i + 1}. {citation.UriCitation.Uri}");
				}
				Console.WriteLine();
			}
			else
			{
				_logger.LogDebug("No citations found for current thread");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to display citations for agent '{AgentName}'", _agentOptions.Name);
		}
	}

	public async Task<bool> ValidateConnectionsAsync()
	{
		try
		{
			if (_agentOptions.Tools == null || _agentOptions.Tools.Count == 0)
			{
				_logger.LogInformation("Agent '{AgentName}' has no tools to validate", _agentOptions.Name);
				return true;
			}

			var allValid = true;
			foreach (var tool in _agentOptions.Tools)
			{
				var isValid = await _agentService.ValidateConnectionAsync(tool.ConnectionName);
				if (!isValid)
				{
					_logger.LogWarning("Invalid connection '{ConnectionName}' for tool '{ToolType}' in agent '{AgentName}'",
						tool.ConnectionName, tool.ToolType, _agentOptions.Name);
					allValid = false;
				}
			}

			return allValid;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to validate connections for agent '{AgentName}'", _agentOptions.Name);
			return false;
		}
	}
}
