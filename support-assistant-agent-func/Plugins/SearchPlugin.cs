using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using support_assistant_agent_func.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace support_assistant_agent_func.Plugins;

public class SearchPlugin
{
    private readonly IAzureAISearchService _azureAISearchService;
    private readonly ILogger<SearchPlugin> _logger;

    public SearchPlugin(
        IAzureAISearchService azureAISearchService,
        ILogger<SearchPlugin> logger)
    {
        _azureAISearchService = azureAISearchService;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Searches the knowledge base for solutions to a support ticket.")]
    public async Task<string> SearchKnowledgeBase(
        [Description("Search Text")] string searchText, [Description("The Scope")] string scope)
    {
        
        return "SearchPlugin.SearchKnowledgeBase";
    }
}