using System.ClientModel;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;

namespace AzureAIAgents;

public sealed class DelegatedAzureAgent
{
	private readonly ILogger _logger;
	private readonly DelegatedAgentService _agentService;
	private readonly AgentOptions _agentOptions;
	private PersistentAgent? _agent;
	private PersistentAgentThread? _thread;

	public DelegatedAzureAgent(ILogger<DelegatedAzureAgent> logger, DelegatedAgentService agentService, AgentOptions agentOptions)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
		_agentOptions = agentOptions ?? throw new ArgumentNullException(nameof(agentOptions));
	}

	public async Task InitializeAsync()
	{
		try
		{
			// Initialize the delegated agent service first
			await _agentService.InitializeAsync();

			var currentUser = await _agentService.GetCurrentUserAsync();
			Console.WriteLine($"🔐 Initializing agent with delegated permissions for: {currentUser}");

			// Define the agent using delegated credentials
			_agent = await _agentService.CreateAgentAsync(_agentOptions);

			Console.WriteLine($"✅ Agent '{_agentOptions.Name}' ready with user-level permissions");

			_logger.LogInformation(
				"{AgentName} initialized successfully with delegated permissions. Agent ID: {AgentId}",
				_agentOptions.Name,
				_agent?.Id
			);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Failed to initialize delegated agent: {ex.Message}");
			_logger.LogError(ex, "Failed to initialize {AgentName} with delegated permissions", _agentOptions.Name);
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
			var currentUser = await _agentService.GetCurrentUserAsync();
			Console.WriteLine($"🔍 Processing query as user: {currentUser}");

			_logger.LogInformation("Processing query: {Query}", userQuery);

			// Create a new thread for the conversation
			var thread = await _agentService.CreateThreadAsync();
			_thread = thread;

			// Add user message
			await _agentService.CreateMessageAsync(thread.Id, userQuery);

			// Get response from agent with delegated permissions
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

	// Expose agent options for easier access
	public AgentOptions AgentOptions => _agentOptions;
}