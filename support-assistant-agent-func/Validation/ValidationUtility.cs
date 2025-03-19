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
            You are an AI assistant evaluating the correctness of answers. The 'correctness metric' is a measure of if the generated answer to a given User Question is correct based on the ground truth answer. 
            You will be given the generated answer and the ground truth answer.
            
            You need to compare the generated answer against the ground truth answer and score the answer between one to five using the following rating scale:
            One: The answer is incorrect
            Three: The answer is partially correct, but could be missing some key context or nuance that makes it potentially misleading or incomplete compared to the ground truth answer provided.
            Five: The answer is correct and complete based on the ground truth answer provided.

            You must also provide your reasoning as to why the rating you selected was given.

            The rating value should always be either 1, 3, or 5.

            You will add your thoughts and rating into the thoughts JSON and return the JSON as the response along with the ground truth answer.

            User Question: {validationRequest.question_and_answer[0].question}            
            Ground truth answer: {validationRequest.question_and_answer[0].answer}
            Generated answer: {validationRequest.question_and_answer[0].llmResponse}";

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
            UserQuestion = evaluationResponse!.UserQuestion,
            GeneratedAnswer = evaluationResponse.GeneratedAnswer,
            Rating = evaluationResponse.Rating,
            Thoughts = evaluationResponse.Thoughts,
            GroundTruthAnswer = evaluationResponse.GroundTruthAnswer
        };

        validationRequest.Evaluation = evaluationResponse;
    }
}