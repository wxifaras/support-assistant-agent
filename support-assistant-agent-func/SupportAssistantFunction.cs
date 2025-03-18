using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using support_assistant_agent_func.Extensions;
using support_assistant_agent_func.Models;
using support_assistant_agent_func.Prompts;
using support_assistant_agent_func.Services;
using System.Text.Json;
using support_assistant_agent_func.Validation;

namespace support_assistant_agent_func;

/// <summary>
/// Provides functions for processing and searching a support knowledge base.
/// </summary>
public class SupportAssistantFunction
{
    private readonly ILogger<SupportAssistantFunction> _logger;
    private readonly IAzureAISearchService _azureAISearchService;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chat;
    private readonly IChatHistoryManager _chatHistoryManager;
    private readonly IValidationUtility _validationUtility;
    private readonly bool _useCosmosDbChatHistory;

    public SupportAssistantFunction(
        ILogger<SupportAssistantFunction> logger,
        IAzureAISearchService azureAISearchService,
        Kernel kernel,
        IChatCompletionService chat,
        IChatHistoryManager chatHistoryManager,
        IValidationUtility validationUtility,
        IConfiguration configuration)
    {
        _logger = logger;
        _azureAISearchService = azureAISearchService;
        _kernel = kernel;
        _chat = chat;
        _chatHistoryManager = chatHistoryManager;
        _validationUtility = validationUtility;
        _useCosmosDbChatHistory = bool.TryParse(configuration["UseCosmosDbChatHistory"], out bool result) ? result : false;
    }

    /// <summary>
    /// Processes a knowledge base file uploaded to blob storage.
    /// </summary>
    /// <param name="stream">The stream containing the knowledge base data.</param>
    /// <param name="name">The name of the blob.</param>
    /// <returns>An action result indicating success or failure.</returns>
    [Function("ProcessKnowledgeBase")]
    public async Task<IActionResult> ProcessKnowledgeBase([BlobTrigger("%KnowledgebaseContainer%/{name}", Connection = "AzureStorageConnectionString")] Stream stream, string name)
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

            knowledgeBase!.Summary = await GetCommentsSummaryAsync(knowledgeBase.comments);

            await _azureAISearchService.IndexKnowledgeBaseAsync(knowledgeBase);
            return new OkObjectResult($"Successfully processed knowledge base: {name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing knowledge base");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Searches the knowledge base for information related to the user's query.
    /// </summary>
    /// <param name="req">The HTTP request containing the search parameters.</param>
    /// <returns>A response containing the search results.</returns>
    /// <remarks>
    /// The request body should be a JSON object with the following structure:
    /// {
    ///   "SessionId": "",
    ///   "Scope": "",
    ///   "SearchText": ""
    /// }
    /// 
    /// Where:
    ///   - SessionId: A unique identifier for the user's session.
    ///   - Scope: The scope, which is used as a security filter in the search
    ///   - SearchText: The user's query or search terms.
    /// </remarks>
    [Function("SearchKnowledgeBase")]
    public async Task<IActionResult> SearchKnowledgeBase(
    [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        if (req == null)
        {
            return new BadRequestObjectResult("Request cannot be null");
        }

        var requestBody = string.Empty;
        using (var streamReader = new StreamReader(req.Body))
        {
            requestBody = await streamReader.ReadToEndAsync();
        }

        SearchRequest searchRequest;
        try
        {
            searchRequest = JsonSerializer.Deserialize<SearchRequest>(requestBody)!;
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

        try
        {
            var sessionId = searchRequest.SessionId.ToString();
            var chatHistory = await _chatHistoryManager.GetOrCreateChatHistoryAsync(sessionId);

            chatHistory.AddUniqueMessage(AuthorRole.User, $"searchText:{searchRequest.SearchText}");
            chatHistory.AddUniqueMessage(AuthorRole.User, $"scope:{searchRequest.Scope}");

            _logger.LogInformation($"searchRequest:{searchRequest}");

            ChatMessageContent? result = await _chat.GetChatMessageContentAsync(
                  chatHistory,
                  executionSettings: new OpenAIPromptExecutionSettings { Temperature = 0.8, TopP = 0.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions },
                  kernel: _kernel);

            chatHistory.AddUniqueMessage(AuthorRole.Assistant, result.Content!);

            if (_useCosmosDbChatHistory)
            {
                await _chatHistoryManager.SaveChatHistoryAsync(sessionId, chatHistory);
            }

            return new OkObjectResult(result.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching knowledge base");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
      
    /// <summary>
    /// Tests the ground truth of a given input.
    /// </summary>
    /// <param name="req">The HTTP request containing the test parameters.</param>
    /// <returns>A response indicating the result of the test.</returns>
    /// <remarks>
    /// The request can either be a JSON object or a multipart/form-data containing key:"file" value:a JSON file with an array of the objects below.
    /// The JSON object will be the following structure:
    ///  {
    ///    "problem_id": "",
    ///    "scope": [""],
    ///    "question_and_answer": [
    ///     {
    ///        "question": "",
    ///        "answer": ""
    ///      }
    ///    ]
    ///}
    /// Where:
    ///   - problem_id: The document for which the search is being performed.
    ///   - scope: The scope, which is used as a security filter in the search.
    ///   - question: The ground truth question.
    ///   - answer: The ground truth answer.
    /// </remarks>
    [Function("TestGroundTruth")]
    public async Task<IActionResult> TestGroundTruth(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        if (req == null)
        {
            return new BadRequestObjectResult("Request cannot be null");
        }

        var validationRequests = new List<ValidationRequest>();
        var fileContent = string.Empty;

        if (req.HasFormContentType)
        {
            var form = await req.ReadFormAsync();
            var file = form.Files.GetFile("file");

            if (file == null || file.Length == 0)
            {
                return new BadRequestObjectResult("File cannot be null or empty");
            }

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            fileContent = await reader.ReadToEndAsync();

            try
            {
                validationRequests = JsonSerializer.Deserialize<List<ValidationRequest>>(fileContent)!;
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Failed to deserialize JSON file: {ex.Message}");
                return new BadRequestObjectResult("Invalid JSON file");
            }
        }
        else
        {
            var requestBody = string.Empty;
            requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            try
            {
                var testRequest = JsonSerializer.Deserialize<ValidationRequest>(requestBody)!;
                validationRequests.Add(testRequest);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Failed to deserialize request body: {ex.Message}");
                return new BadRequestObjectResult("Invalid request payload");
            }
        }

        if (validationRequests.Count== 0)
        {
            _logger.LogError("Request body and file content are both null or empty");
            return new BadRequestObjectResult("Request body and file content cannot be both null or empty");
        }

        await CallKernelWithValidationRequestsAsync(validationRequests);

        return new OkObjectResult(validationRequests);
    }

    private async Task CallKernelWithValidationRequestsAsync(List<ValidationRequest> validationRequests)
    {   
        foreach (var request in validationRequests)
        {
            var chatHistory = new ChatHistory();
            request.SearchText= request.question_and_answer[0].question;
            var scope = request.scope != null ? string.Join(", ", request.scope) : string.Empty;

            chatHistory.AddUniqueMessage(AuthorRole.User, $"searchText:{request.SearchText}");
            chatHistory.AddUniqueMessage(AuthorRole.User, $"scope:\"{scope}\"");

            var result = await _chat.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new OpenAIPromptExecutionSettings { Temperature = 0.0, TopP = 0.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions },
                kernel: _kernel);

            request.question_and_answer[0].llmResponse = result.Content;

            await _validationUtility.EvaluateSearchResultAsync(request);
        }
    }

    private async Task<string> GetCommentsSummaryAsync(List<Comment> comments)
    {
        var summaryPrompt = CorePrompts.GetSummaryPrompt(comments);

        ChatHistory history = [];
        history.AddUserMessage(summaryPrompt);
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        var response = await chatCompletionService.GetChatMessageContentAsync(
            history,
            kernel: _kernel
        );

        return response.Content!;
    }
}