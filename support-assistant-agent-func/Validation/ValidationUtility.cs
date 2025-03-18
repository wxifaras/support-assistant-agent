using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using support_assistant_agent_func.Models;
using System.Text.Json;

namespace support_assistant_agent_func.Validation;

public interface IValidationUtility
{
  Task EvaluateSearchResultAsync(ValidationRequest validationRequest);
}

public class ValidationUtility: IValidationUtility
{
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly string _azureOpenAIDeployment;

    public ValidationUtility(AzureOpenAIClient azureOpenAIClient, IOptions<AzureOpenAIOptions> azureOpenAIOptions)
    {
        _azureOpenAIClient = azureOpenAIClient;
        _azureOpenAIDeployment = azureOpenAIOptions.Value.AzureOpenAIDeployment ?? throw new ArgumentNullException(nameof(azureOpenAIOptions.Value.AzureOpenAIDeployment));
    }

    public async Task EvaluateSearchResultAsync(ValidationRequest validationRequest)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory; 
        var evaluationSchemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Validation", "EvaluationSchema.json");
        var evaluationSchema = await File.ReadAllTextAsync(evaluationSchemaPath);

        var evaluationPrompt = $@"
            You are an AI assistant evaluating the correctness of answers.
            Here is the ground truth answer: {validationRequest.question_and_answer[0].answer}
            Check the ground truth answer with the generated answer from the model which is Response: {validationRequest.question_and_answer[0].llmResponse}
             The rating value should always be either 1, 3, or 5.
                 One: The answer is incorrect
                 Three: The answer is partially correct, but could be missing some key context or nuance that makes it potentially misleading or incomplete compared to the context provided.
                 Five: The answer is correct and complete based on the context provided.
             User Query: {validationRequest.question_and_answer[0].question} 
             Return a JSON object with the following scores:
                 -accuracy_score: Measures how factually correct the response is (1, 3, or 5).
                 -completeness_score: Measures if the response covers all relevant points(1, 3, or 5).
                 -relevance_score: Measures how well the response aligns with the user query(1, 3, or 5).
                 -thoughtprocess: You will add your thoughts and rating for each accuracy_score,completeness_score and relevance_score into the thoughtprocess JSON and return the JSON as the response.
             JSON should be well formed.
             The rating value should always be either 1, 3, or 5.";

        var client = _azureOpenAIClient.GetChatClient(_azureOpenAIDeployment);

        var chat = new List<ChatMessage>()
        {
          new SystemChatMessage(evaluationPrompt)
        };

        var chatUpdates = await client.CompleteChatAsync(
            chat,
            new ChatCompletionOptions()
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("Eval", BinaryData.FromString(evaluationSchema))
            });

        var evaluationResponse = JsonSerializer.Deserialize<Evaluation>(chatUpdates.Value.Content[0].Text);

        evaluationResponse = new Evaluation
        {
            UserQuestion = evaluationResponse.UserQuestion,
            GeneratedAnswer = evaluationResponse.GeneratedAnswer,
            AccuracyScore = evaluationResponse.AccuracyScore,
            CompletenessScore = evaluationResponse.CompletenessScore,
            RelevanceScore = evaluationResponse.RelevanceScore,
            ThoughtProcess = new ThoughtProcess
            {
                AccuracyScore = evaluationResponse.ThoughtProcess.AccuracyScore,
                CompletenessScore = evaluationResponse.ThoughtProcess.CompletenessScore,
                RelevanceScore = evaluationResponse.ThoughtProcess.RelevanceScore
            },
            GroundTruthAnswer = evaluationResponse.GroundTruthAnswer
        };

        validationRequest.Evaluation = evaluationResponse;
    }
}
