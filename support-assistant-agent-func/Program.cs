using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using support_assistant_agent_func.Models;
using support_assistant_agent_func.Plugins;
using support_assistant_agent_func.Prompts;
using support_assistant_agent_func.Services;
using Azure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Services.AddOptions<AzureOpenAIOptions>()
           .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.AzureOpenAI))
           .ValidateDataAnnotations();

builder.Services.AddOptions<AzureAISearchOptions>()
           .Bind(builder.Configuration.GetSection(AzureAISearchOptions.AzureAISearch))
           .ValidateDataAnnotations();

builder.Services.AddSingleton(sp =>
{
    var azureAISearchOptions = sp.GetRequiredService<IOptions<AzureAISearchOptions>>();
    var logger = sp.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Initializing Search Index Client with endpoint: {Endpoint}", azureAISearchOptions.Value.SearchServiceEndpoint);
    return new SearchIndexClient(new Uri(azureAISearchOptions.Value.SearchServiceEndpoint!), new AzureKeyCredential(azureAISearchOptions.Value.SearchAdminKey!));
});

builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var azureOpenAIOptions = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>();

    logger.LogInformation("Initializing OpenAI Client with endpoint: {Endpoint}", azureOpenAIOptions.Value.AzureOpenAIEndPoint);
    return new AzureOpenAIClient(new Uri(azureOpenAIOptions.Value.AzureOpenAIEndPoint!), new AzureKeyCredential(azureOpenAIOptions.Value.AzureOpenAIKey!));
});

builder.Services.AddSingleton<IAzureAISearchService>(sp =>
{
    var azureAISearchOptions = sp.GetRequiredService<IOptions<AzureAISearchOptions>>();
    var azureOpenAIOptions = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>();
    var logger = sp.GetRequiredService<ILogger<AzureAISearchService>>();
    var searchIndexClient = sp.GetRequiredService<SearchIndexClient>();
    var azureOpenAIClient = sp.GetRequiredService<AzureOpenAIClient>();
    return new AzureAISearchService(logger, azureAISearchOptions, azureOpenAIOptions, searchIndexClient, azureOpenAIClient);
});

builder.Services.AddSingleton<Kernel>(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    var kernelOptions = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;

    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: kernelOptions.AzureOpenAIDeployment,
        endpoint: kernelOptions.AzureOpenAIEndPoint,
        apiKey: kernelOptions.AzureOpenAIKey
    );

    var azureAISearchService = provider.GetRequiredService<IAzureAISearchService>();

    var logger = provider.GetRequiredService<ILogger<SearchPlugin>>();
    var searchPlugin = new SearchPlugin(azureAISearchService, logger);
    kernelBuilder.Plugins.AddFromObject(searchPlugin, "SearchPlugin");

    return kernelBuilder.Build();
});

builder.Services.AddSingleton<IChatCompletionService>(sp =>
         sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

builder.Services.AddSingleton<IChatHistoryManager>(sp =>
{
    var sysPrompt = CorePrompts.GetSystemPrompt();
    return new ChatHistoryManager(sysPrompt);
});

builder.Build().Run();