# Azure AI Agents

A comprehensive demonstration of Azure OpenAI Agents using .NET.

## Features
* Basic Bing Search Grounding: Full implementation of Azure OpenAI Agents with web search capabilities
* Custom Search Integration: Advanced configuration for domain-specific AI agents
* SharePoint Grounding: Pull SharePoint contents
* Flexible Architecture: Maintainable, configuration-driven design supporting multiple agent types
* Real-time Information Access: Agents that combine trained knowledge with current web data
* Enterprise-Ready: Production-grade error handling, logging, and configuration management

## 📖 Blog Posts

For a detailed step-by-step guide, read the complete blog posts:

**[Migrating from Bing Search APIs to Azure OpenAI Agent with Grounding Bing Search](https://codingwithramin.com/?p=493)**

**[Beyond Basic Web Search: Building a Specialized AI Agent with Bing Custom Search](https://codingwithramin.com/?p=504)**

**[Beyond Manual Indexing: Building Intelligent SharePoint Agents – Part 1: The Foundation](https://codingwithramin.com/?p=518)**

**[Beyond Manual Indexing: Building Intelligent SharePoint Agents – Part 2: Implementation](https://codingwithramin.com/?p=532)**

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
   - Set up Azure AI Foundry and project
   - Create "Grounding with Bing Search" resource for basic search
   - Create "Grounding with Bing Custom Search" resource for specialized search
   - Connect both resources to your AI Foundry project
   - Create a "SharePoint Grouding" connection
   
3. **Configure the application**
   ```bash
   cd AzureAIAgents
   cp appsettings.json.template appsettings.json
   # Edit appsettings.json with your Azure credentials
   ```

4. **Run the sample**
   ```bash
   dotnet run
   ```

## 🛠️ NuGet Packages

The project uses the following key packages:

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
---

⭐ If this repository helped you with your migration, please give it a star!

📝 **Last updated:** 16/07/2025
🏷️ **Version:** 1.0.0