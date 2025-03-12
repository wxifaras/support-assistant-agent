﻿using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using support_assistant_agent_func.Services;
using System.ComponentModel;

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
    public async Task<List<SearchDocument>> SearchKnowledgeBase(
    [Description("Search Text")] string searchText, [Description("The Scope")] string scope)
    {
        _logger.LogInformation($"SearchPlugin.SearchKnowledgeBase invoked. searchText:'{searchText}' in scope:'{scope}'");
        var knowledgeBaseList = await _azureAISearchService.SearchKnowledgeBaseAsync(scope, searchText);

        _logger.LogInformation($"SearchPlugin.SearchKnowledgeBase found {knowledgeBaseList.Count} results");

        return knowledgeBaseList;
    }
}