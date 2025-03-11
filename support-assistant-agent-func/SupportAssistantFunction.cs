using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using support_assistant_agent_func.Models;
using support_assistant_agent_func.Services;
using System.Text.Json;

namespace support_assistant_agent_func;

public class SupportAssistantFunction
{
    private readonly ILogger<SupportAssistantFunction> _logger;
    private readonly IAzureAISearchService _azureAISearchService;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chat;
    private readonly IChatHistoryManager _chatHistoryManager;

    public SupportAssistantFunction(
        ILogger<SupportAssistantFunction> logger,
        IAzureAISearchService azureAISearchService,
        Kernel kernel,
        IChatCompletionService chat,
        IChatHistoryManager chatHistoryManager)
    {
        _logger = logger;
        _azureAISearchService = azureAISearchService;
        _kernel = kernel;
        _chat = chat;
        _chatHistoryManager = chatHistoryManager;
    }

    [Function("ProcessKnowledgeBase")]
    public async Task ProcessKnowledgeBase([BlobTrigger("%KnowledgebaseContainer%/{name}", Connection = "AzureStorageConnectionString")] Stream stream, string name)
    {
        try
        {
            using var blobStreamReader = new StreamReader(stream);
            var content = await blobStreamReader.ReadToEndAsync();

            _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var knowledgeBase = JsonSerializer.Deserialize<KnowledgeBase>(content, options);

            // index the knowledge base document
            await _azureAISearchService.IndexKnowledgeBaseAsync(knowledgeBase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing knowledge base");
        }
    }

    [Function("SearchKnowledgeBase")]
    public async Task<IActionResult> SearchKnowledgeBase(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        var requestBody = string.Empty;
        using (var streamReader = new StreamReader(req.Body))
        {
            requestBody = await streamReader.ReadToEndAsync();
        }

        SearchRequest searchRequest;
        try
        {
            searchRequest = JsonSerializer.Deserialize<SearchRequest>(requestBody);
        }
        catch (JsonException ex)
        {
            _logger.LogError($"Failed to deserialize request body: {ex.Message}");
            return new BadRequestObjectResult("Invalid request payload");
        }

        if (searchRequest == null)
        {
            _logger.LogError("Request body is null or empty");
            return new BadRequestObjectResult("Request body cannot be null or empty");
        }

        var sessionId = searchRequest.SessionId;
        var chatHistory = _chatHistoryManager.GetOrCreateChatHistory(sessionId.ToString());
        chatHistory.AddUserMessage($"searchText:{searchRequest.SearchText}");
        chatHistory.AddUserMessage($"scope:{searchRequest.Scope}");

        ChatMessageContent? result = await _chat.GetChatMessageContentAsync(
              chatHistory,
              executionSettings: new OpenAIPromptExecutionSettings { Temperature = 0.8, TopP = 0.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions },
              kernel: _kernel);

        return new OkObjectResult(result.Content);
    }
}