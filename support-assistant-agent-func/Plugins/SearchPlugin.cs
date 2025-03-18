using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
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
        [Description("Search Text")] string searchText, [Description("Scope")] string scope)
    {
        _logger.LogInformation($"SearchPlugin.SearchKnowledgeBase invoked. searchText:'{searchText}' in scope:'{scope}'");

        // Fetch search results and convert to KnowledgeBase list
        var knowledgeBaseList = await _azureAISearchService.SearchKnowledgeBaseAsync(scope, searchText);

        _logger.LogInformation($"SearchPlugin.SearchKnowledgeBase found {knowledgeBaseList.Count} results");

        return knowledgeBaseList;
    }
}