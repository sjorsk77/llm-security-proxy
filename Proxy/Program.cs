using System.Numerics;
using llm_protector.protection;
using dotenv.net;
using llm_protector;
using llm_protector.config;
using llm_protector.decorators;
using llm_protector.filters;
using llm_protector.ml_helpers;
using llm_protector.ml;
using llm_protector.protection.filter;
using llm_protector.protection.riskfiles;
using llm_protector.static_filter;
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


builder.Services.AddSingleton<ProxyLogService>();
builder.Services.AddSingleton<RiskFileService>();
builder.Services.AddSingleton<FilterManagementService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<ProxyConfigManager>();
builder.Services.AddSingleton<DecodingService>();
builder.Services.AddSingleton<LogDecorator>();
builder.Services.AddSingleton<OnnxPromptAnalyzer>();
builder.Services.AddSingleton<TokenizerHelper>();
builder.Services.AddSingleton<EmbeddingHelper>();
builder.Services.AddSingleton<PatternFilter>();
builder.Services.AddSingleton<TfIdfFilter>();
builder.Services.AddSingleton<OnnxFilter>();
builder.Services.AddSingleton<VectorFilter>();
builder.Services.AddHostedService<ProxyConfigMonitor>();
builder.Host.UseSerilog();

builder.Services.AddReverseProxy()
    .LoadFromMemory(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());

var app = builder.Build();

var configManager = app.Services.GetRequiredService<ProxyConfigManager>();
configManager.ReloadConfigFromDatabase();

app.UseMiddleware<ProtectionMiddleware>();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<ProtectionMiddleware>();
});

app.Run();