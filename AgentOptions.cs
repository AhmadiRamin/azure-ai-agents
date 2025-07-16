
namespace BingGroundingAgent
{
    public interface IAgentOptions
    {
        string Deployment { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        string Instructions { get; set; }
        List<ToolsOptions>? Tools { get; set; }
    }

    public class ToolsOptions
    {
        public string ConnectionName { get; set; } = string.Empty;
        public string? ConfigurationName { get; set; } = string.Empty;
        public string ToolType { get; set; } = string.Empty;
    }

    public class AgentOptions : IAgentOptions
    {
        public string Deployment { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public List<ToolsOptions>? Tools { get; set; }
    }

    public class AgentsConfiguration
    {
        public List<AgentOptions> Agents { get; set; } = new();
    }
}
