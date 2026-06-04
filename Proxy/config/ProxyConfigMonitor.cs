using Shared.settings;

namespace llm_protector.config;

public class ProxyConfigMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private string _lastKnownUrl = string.Empty;
    
    public ProxyConfigMonitor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var configManager = scope.ServiceProvider.GetRequiredService<ProxyConfigManager>();

            var currentUrl = settings.BackendUrl;
            
            if (string.IsNullOrEmpty(_lastKnownUrl))
            {
                _lastKnownUrl = currentUrl;
                continue;
            }
            
            if (currentUrl != _lastKnownUrl)
            {
                _lastKnownUrl = currentUrl;
                configManager.ReloadConfigFromDatabase();
            }
        }
    }
}