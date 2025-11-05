using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureAIAgents;

public interface IUserAuthenticationService
{
	Task<string> GetUserAccessTokenAsync();
	Task<bool> IsUserAuthenticatedAsync();
	Task SignInAsync();
	Task SignOutAsync();
	Task<string> GetUserIdentityAsync();
}

public class DeviceCodeAuthenticationService : IUserAuthenticationService
{
	private readonly IPublicClientApplication _clientApp;
	private readonly ILogger<DeviceCodeAuthenticationService> _logger;
	private readonly string[] _scopes =
	{
		"https://ai.azure.com/user_impersonation"
    };

	public DeviceCodeAuthenticationService(IConfiguration configuration, ILogger<DeviceCodeAuthenticationService> logger)
	{
		_logger = logger;

		var clientId = configuration["AzureAd:ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId not configured");
		var tenantId = configuration["AzureAd:TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId not configured");

		_clientApp = PublicClientApplicationBuilder
			.Create(clientId)
			.WithAuthority($"https://login.microsoftonline.com/{tenantId}")
			.WithDefaultRedirectUri()
			.Build();
	}

	public async Task<string> GetUserAccessTokenAsync()
	{
		try
		{
			var accounts = await _clientApp.GetAccountsAsync();
			var firstAccount = accounts.FirstOrDefault();

			AuthenticationResult result;

			if (firstAccount != null)
			{
				try
				{
					result = await _clientApp.AcquireTokenSilent(_scopes, firstAccount)
						.ExecuteAsync();
					_logger.LogInformation("Successfully acquired token from cache for user: {Username}", firstAccount.Username);
					return result.AccessToken;
				}
				catch (MsalUiRequiredException)
				{
					_logger.LogInformation("Silent token acquisition failed, using device code flow");
				}
			}

			result = await _clientApp.AcquireTokenWithDeviceCode(_scopes, deviceCodeResult =>
			{
				Console.WriteLine();
				Console.WriteLine("🔐 Authentication Required");
				Console.WriteLine("─────────────────────────────────────────────────────");
				Console.WriteLine($"Please open a web browser and navigate to:");
				Console.WriteLine($"🌐 {deviceCodeResult.VerificationUrl}");
				Console.WriteLine();
				Console.WriteLine($"Enter this code: {deviceCodeResult.UserCode}");
				Console.WriteLine("─────────────────────────────────────────────────────");
				Console.WriteLine("✋ Waiting for you to complete authentication...");
				Console.WriteLine();

				return Task.FromResult(0);
			}).ExecuteAsync();

			_logger.LogInformation("Successfully authenticated user: {Username}", result.Account.Username);
			return result.AccessToken;
		}
		catch (MsalException ex)
		{
			_logger.LogError(ex, "Failed to acquire user access token");
			throw;
		}
	}

	public async Task<bool> IsUserAuthenticatedAsync()
	{
		var accounts = await _clientApp.GetAccountsAsync();
		return accounts.Any();
	}

	public async Task SignInAsync()
	{
		await GetUserAccessTokenAsync();
	}

	public async Task SignOutAsync()
	{
		var accounts = await _clientApp.GetAccountsAsync();
		foreach (var account in accounts)
		{
			await _clientApp.RemoveAsync(account);
		}
		_logger.LogInformation("User signed out successfully");
	}

	public async Task<string> GetUserIdentityAsync()
	{
		var accounts = await _clientApp.GetAccountsAsync();
		return accounts.FirstOrDefault()?.Username ?? "Unknown User";
	}
}