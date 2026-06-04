using System.CodeDom;
using llm_protector.protection;
using dotenv.net;
using llm_protector;
using llm_protector.config;
using llm_protector.ml;
using llm_protector.protection.filter;
using llm_protector.protection.riskfiles;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Shared;
using Shared.settings;
using Yarp.ReverseProxy.Configuration;

DotEnv.Load();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.GrafanaLoki(
        uri: "http://localhost:3100", 
        labels: new[] { new LokiLabel { Key = "app", Value = "llm-security-proxy" } }
    )
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();


builder.Services.AddSingleton<PromptProtectionService>();
builder.Services.AddSingleton<ProxyLogService>();
builder.Services.AddSingleton<RiskFileService>();
builder.Services.AddSingleton<FilterManagementService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<ProxyConfigManager>();
builder.Services.AddSingleton<PromptMlClassifier>();
builder.Services.AddSingleton<DecodingService>();
builder.Services.AddHostedService<ProxyConfigMonitor>();
builder.Host.UseSerilog();

builder.Services.AddReverseProxy()
    .LoadFromMemory(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());

var app = builder.Build();

var configManager = app.Services.GetRequiredService<ProxyConfigManager>();
configManager.ReloadConfigFromDatabase();

app.MapReverseProxy(proxyPipeline =>
{
    var protectionService = app.Services.GetRequiredService<PromptProtectionService>();
    var settingsService = app.Services.GetRequiredService<SettingsService>();
    var logService = app.Services.GetRequiredService<ProxyLogService>();
    
    proxyPipeline.Use(async (context, next) =>
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (settingsService.IsFilterActive && protectionService.PassRequest(body, out var blockReason))
        {
            logService.AddLogEntry(body, isBlocked: true, reason: blockReason);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Safety Guard: Request blocked due to unsafe detection.");
            return; 
        }

        logService.AddLogEntry(body, isBlocked: false);
        await next();
    });
});

app.Run();