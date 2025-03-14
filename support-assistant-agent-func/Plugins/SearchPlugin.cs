using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using support_assistant_agent_func.Models;
using support_assistant_agent_func.Services;
using System.ComponentModel;

namespace support_assistant_agent_func.Plugins;

public class SearchPlugin
{
    private readonly IAzureAISearchService _azureAISearchService;
    private readonly ILogger<SearchPlugin> _logger;
    private Kernel? _kernel;

    public SearchPlugin(
        IAzureAISearchService azureAISearchService,
        ILogger<SearchPlugin> logger)
    {
        _azureAISearchService = azureAISearchService;
        _logger = logger;
    }

    public void SetKernel(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    [KernelFunction]
    [Description("Searches the knowledge base for solutions to a support ticket.")]
    public async Task<object> SearchKnowledgeBase(
        [Description("Search Text")] string searchText, [Description("The Scope")] string scope, [Description("Is Evaluation Required")] bool isEvalRequired)
    {
        _logger.LogInformation($"SearchPlugin.SearchKnowledgeBase invoked. searchText:'{searchText}' in scope:'{scope}'");

        // Fetch search results and convert to KnowledgeBase list
        var knowledgeBaseList = await _azureAISearchService.SearchKnowledgeBaseAsync(scope, searchText);

        _logger.LogInformation($"SearchPlugin.SearchKnowledgeBase found {knowledgeBaseList.Count} results");

        return knowledgeBaseList;
    }

    private static KnowledgeBase ConvertToKnowledgeBase(SearchDocument doc)
    {
        return new KnowledgeBase
        {
            problem_id = doc["problem_id"]?.ToString(),
            description = doc["description"]?.ToString(),
            status = doc["status"]?.ToString(),
           /* impact = doc["impact"]?.ToString(),
            category = doc["category"]?.ToString(),
            reported_date = DateTime.TryParse(doc["reported_date"]?.ToString(), out var rd) ? rd : DateTime.MinValue,
            resolved_date = DateTime.TryParse(doc["resolved_date"]?.ToString(), out var rld) ? rld : null,
            assigned_to = doc["assigned_to"]?.ToString(),
            reported_by = doc["reported_by"]?.ToString(),*/
            root_cause = doc["root_cause"]?.ToString(),
            workaround = doc["workaround"]?.ToString(),
            resolution = doc["resolution"]?.ToString(),
            title = doc["title"]?.ToString()
           /* related_incidents = doc["related_incidents"]?.ToObject<List<string>>(),
            Scope = doc["Scope"]?.ToObject<List<string>>(),
            attachments = doc["attachments"]?.ToObject<List<Attachment>>(),
            comments = doc["comments"]?.ToObject<List<Comment>>()*/
        };
    }

   
}