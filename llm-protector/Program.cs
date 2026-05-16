using llm_protector.protection;
using dotenv.net;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddSingleton<PromptProtectionService>();

var app = builder.Build();

var backendUrl = app.Configuration["ReverseProxy:Clusters:llm-cluster:Destinations:openai:Address"];
Console.WriteLine($"De proxy stuurt nu alles naar: {backendUrl}");

app.MapReverseProxy(proxyPipeline =>
{
    var protectionService = app.Services.GetRequiredService<PromptProtectionService>();
    proxyPipeline.Use(async (context, next) =>
    {
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);

        var body = await reader.ReadToEndAsync();

        context.Request.Body.Position = 0;
        
        Console.WriteLine($"[PROXY] Inkomende body: {body}");
        
        //Validate input
        if (protectionService.IsNotSafe(body))
        {
            Console.WriteLine("[SECURITY] Blocked request");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Safety Guard: Request blocked due to unsafe detection.");
            return; 
        }

        await next();
        
        //Validate output
    });
});

/*app.UseHttpsRedirection();*/

app.Run();