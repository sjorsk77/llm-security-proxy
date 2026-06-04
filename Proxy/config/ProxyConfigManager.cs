using Shared.settings;
using Yarp.ReverseProxy.Configuration;

namespace llm_protector.config;

public class ProxyConfigManager
{
    private readonly InMemoryConfigProvider _configProvider;
    private readonly SettingsService _settingsService;

    public ProxyConfigManager(InMemoryConfigProvider configProvider, SettingsService settingsService)
    {
        _configProvider = configProvider;
        _settingsService = settingsService;
    }
    
    public void ReloadConfigFromDatabase()
    {
        var backendUrl = _settingsService.BackendUrl;
        
        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = "llm-route",
                ClusterId = "llm-cluster",
                Match = new RouteMatch { Path = "{**catch-all}" }
            }
        };
        
        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = "llm-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    { 
                        "openai", 
                        new DestinationConfig { Address = backendUrl } 
                    }
                }
            }
        };
        
        _configProvider.Update(routes, clusters);
    }
}