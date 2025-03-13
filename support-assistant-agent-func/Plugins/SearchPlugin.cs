using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using support_assistant_agent_func.Models;
using support_assistant_agent_func.Services;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;
using Azure;
using OpenAI.Assistants;
using System.Text.RegularExpressions;

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

    [KernelFunction]
    public async Task<Object> EvaluateSearchResult(
        [Description("Search Text")] string searchText, [Description("Problem Id")] string pId, [Description("LLM Result")] string llmresult) {
        
        
            // Load Ground Truth Data
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string groundTruthFileName = Path.Combine(baseDirectory, "GroundTruthData", "Ground_Truth_Doc 1.json");

            string jsonContent = File.ReadAllText(groundTruthFileName);

            // Deserialize into a generic object or a strongly typed model
            using var document = JsonDocument.Parse(jsonContent);
            JsonElement root = document.RootElement;

        string? problemId = null;
        string? groundTruthResponse = null;

        foreach (var item in root.EnumerateArray())
        {
            problemId = item.GetProperty("problem_id").GetString();
            foreach (var qa in item.GetProperty("question_and_answer").EnumerateArray())
            {
                string question = qa.GetProperty("question").GetString() ?? string.Empty;
                
                if (problemId.Equals(pId))
                {
                    groundTruthResponse = qa.GetProperty("answer").GetString();
                    break;
                }
            }

            if (problemId != null) break;
        }



        // Evaluate LLM Response Against Ground Truth
        var evaluationFunction = _kernel.CreateFunctionFromPrompt(
                """
     You are an AI assistant evaluating the correctness of answers.

     Response: {llmResponse}
     Ground Truth: {groundTruthResponse}

     Return a JSON object with the following scores:
     - accuracy_score: Measures how factually correct the response is (0-10).
     - completeness_score: Measures if the response covers all relevant points (0-10).
     - relevance_score: Measures how well the response aligns with the query (0-10).
     - thoughtprocess: You will add your thoughts and rating for each accuracy_score,completeness_score and relevance_score into the thoughtprocess JSON and return the JSON as the response.
     JSON should be well formed.
     """,
                new OpenAIPromptExecutionSettings { MaxTokens = 150, Temperature = 0.0, TopP = 0.0 });

            var evalInput = new KernelArguments
            {
                ["llmResponse"] = llmresult,
                ["groundTruthResponse"] = groundTruthResponse // Ensure proper JSON format
            };

        string evaluationJson = (await _kernel.InvokeAsync(evaluationFunction, evalInput)).ToString();
        //var evaluationResult = JsonSerializer.Deserialize<EvaluationScores>(evaluationJson);

        // Construct Final Response JSON
        var responseJson = new KnowledgeBase
        {
            problem_id = problemId ?? Guid.NewGuid().ToString(),
            description = searchText,
            resolution = llmresult,
            Evaluation = evaluationJson
        };

        return responseJson;
        
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