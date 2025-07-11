using Azure.AI.Agents.Persistent;
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
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.AddSingleton<BingGroundingAgent.BingGroundingAgent>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var agent = host.Services.GetRequiredService<BingGroundingAgent.BingGroundingAgent>();

try
{
    logger.LogInformation("Initializing Bing Grounding Agent...");
    await agent.InitializeAsync();

    while (true)
    {
        Console.Write("\nYou: ");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
            break;

        logger.LogInformation("Processing query: {Query}", input);

        Console.Write("Assistant: ");
        await foreach (var update in await agent.ProcessQueryAsync(input))
        {
            if (update.UpdateKind == StreamingUpdateReason.MessageUpdated)
            {
                MessageContentUpdate messageContent = (MessageContentUpdate)update;
                Console.Write(messageContent.Text);
            }
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred: {Message}", ex.Message);
}

logger.LogInformation("Application completed");
await host.StopAsync();
