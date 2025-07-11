# Azure OpenAI Agent with Bing Grounding Migration

Complete migration guide and sample code for transitioning from Bing Search APIs to Azure OpenAI Agent with Grounding Bing Search using Semantic Kernel.

## 🚨 Important Notice

**Bing Search APIs are expiring on August 11, 2025.** This repository provides a complete migration path to Azure OpenAI Agent with Grounding Bing Search capabilities.

## 📖 Blog Post

For a detailed step-by-step guide, read the complete blog post:

**[Migrating from Bing Search APIs to Azure OpenAI Agent with Grounding Bing Search](https://codingwithramin.com/?p=493)**

## 🚀 Quick Start

### Prerequisites

- Azure subscription
- Access to Azure OpenAI services
- .NET 8.0 or later
- Visual Studio or Visual Studio Code

### Setup Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/AhmadiRamin/semantic-kernel-bing-grounding-demo.git
   cd azure-openai-bing-grounding-migration
   ```

2. **Create Azure Resources**
   - Set up Azure AI Foundry hub and project
   - Create a Grounding with Bing Search resource
   - Deploy Azure OpenAI model (GPT-4o recommended)

3. **Configure the application**
   ```bash
   cd BingGroundingAgent
   cp appsettings.json.template appsettings.json
   # Edit appsettings.json with your Azure credentials
   ```

4. **Run the sample**
   ```bash
   dotnet run
   ```

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────────┐
│   Your App     │───▶│ Semantic Kernel  │───▶│   Azure AI Agent    │
└─────────────────┘    └──────────────────┘    └─────────────────────┘
                                                          │
                                                          ▼
                                                ┌─────────────────────┐
                                                │ Grounding with Bing │
                                                │    Search Tool      │
                                                └─────────────────────┘
```

## 🔧 Configuration

### Required Settings

Update your `appsettings.json` with the following:

```json
{
  "AzureAI": {
    "ConnectionString": "your-azure-ai-project-connection-string"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-resource-name.openai.azure.com",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4o"
  },
  "BingGrounding": {
    "ConnectionName": "your-bing-connection-name"
  }
}
```

## 🔍 Usage Examples

### Basic Query
```csharp
var response = await agent.ProcessQueryAsync("What are the latest developments in AI technology?");
```

### Current Events
```csharp
var response = await agent.ProcessQueryAsync("What is the current weather in Seattle?");
```

### Technical Information
```csharp
var response = await agent.ProcessQueryAsync("What are the newest features in .NET 8?");
```

## 🆚 Migration Comparison

| Feature | Bing Search APIs | Azure AI Agent with Bing Grounding |
|---------|------------------|-------------------------------------|
| **Integration** | Manual HTTP calls | Automatic via Semantic Kernel |
| **Response Format** | Raw JSON | Conversational responses |
| **Citations** | Manual implementation | Automatic with source links |
| **Rate Limiting** | Separate API limits | Azure OpenAI quotas |
| **Cost Management** | Separate billing | Consolidated Azure billing |
| **Complexity** | High (manual processing) | Low (automatic processing) |

## 📚 Documentation

- [Azure AI Foundry Documentation](https://docs.microsoft.com/azure/ai-foundry)
- [Semantic Kernel Documentation](https://docs.microsoft.com/semantic-kernel)
- [Azure OpenAI Agent Documentation](https://docs.microsoft.com/azure/cognitive-services/openai/agents)
- [Grounding with Bing Search Guide](https://learn.microsoft.com/azure/ai-foundry/agents/how-to/tools/bing-grounding)

## 🛠️ NuGet Packages

The project uses the following key packages:

- `Microsoft.SemanticKernel` (1.60.0)
- `Microsoft.SemanticKernel.Agents.OpenAI` (1.60.0-preview)
- `Azure.AI.Projects` (v1.0.0-beta.9)
- `Azure.AI.Agents.Persistent` (1.1.0-beta.3)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request


## 🔗 Related Resources

- [Original Blog Post](https://codingwithramin.com/?p=493) - Complete migration guide
- [Bing Search APIs Deprecation Notice](https://docs.microsoft.com/bing/search-apis/migration)
- [Azure OpenAI Service](https://azure.microsoft.com/products/ai-services/openai-service)
- [Semantic Kernel GitHub](https://github.com/microsoft/semantic-kernel)

---

⭐ If this repository helped you with your migration, please give it a star!

📝 **Last updated:** 11/07/2025
🏷️ **Version:** 1.0.0