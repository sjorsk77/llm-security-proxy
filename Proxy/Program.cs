using llm_protector.protection;
using dotenv.net;
using llm_protector;
using llm_protector.protection.filter;
using llm_protector.protection.riskfiles;
using Shared;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddSingleton<PromptProtectionService>();
builder.Services.AddSingleton<ProxyLogService>();
builder.Services.AddSingleton<RiskFileService>();
builder.Services.AddSingleton<FilterManagementService>();
builder.Services.AddSingleton<DatabaseService>();

var app = builder.Build();

var backendUrl = app.Configuration["ReverseProxy:Clusters:llm-cluster:Destinations:openai:Address"];
Console.WriteLine($"De proxy stuurt nu alles naar: {backendUrl}");

app.MapReverseProxy(proxyPipeline =>
{
    var protectionService = app.Services.GetRequiredService<PromptProtectionService>();
    var logService = app.Services.GetRequiredService<ProxyLogService>();
    
    proxyPipeline.Use(async (context, next) =>
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (logService.IsFilterActive && protectionService.IsNotSafe(body, out var blockReason))
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