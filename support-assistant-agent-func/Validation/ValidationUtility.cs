using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using support_assistant_agent_func.Models;
using System.Text.Json;

namespace support_assistant_agent_func.Validation;

public interface IValidationUtility
{
    Task <ValidationResponse> EvaluateSearchResultAsync(ValidationRequest validationRequest);
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

    public async Task <ValidationResponse> EvaluateSearchResultAsync(ValidationRequest validationRequest)
    {
        string baseDirectory;
        string evaluationSchemaPath;
        string evaluationSchema;
        string evaluationPrompt;
        ValidationResponse validationResponse = new ValidationResponse();

        if (validationRequest.isProductionEvaluation)
        {
            baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            evaluationSchemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Validation", "ProductionEvaluationSchema.json");
            evaluationSchema = await File.ReadAllTextAsync(evaluationSchemaPath);

            evaluationPrompt = $@"
            You are an AI assistant evaluating the correctness of answers in a production environment. The 'correctness metric' is a measure of if the generated answer to a given User Question is correct based on the Knowledge Base Document. 

            You need to compare the generated answer against the knowledge base document and score the answer between one to five using the following rating scale:
            One: The answer is incorrect
            Three: The answer is partially correct, but could be missing some key context or nuance that makes it potentially misleading or incomplete compared to the knowledge base document provided.
            Five: The answer is correct and complete based on the knowledge base document provided.

            You must also provide your reasoning as to why the rating you selected was given.

            The rating value should always be either 1, 3, or 5.

            You will add your thoughts, rating, and knowledge base doucment into the thoughts JSON and return the JSON as the response.

            User Question: {validationRequest.question_and_answer[0].question}            
            Generated answer: {validationRequest.question_and_answer[0].llmResponse}
            Knowledge Base Document: {validationRequest.knowledgeBase}";
        }
        else
        {
            baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            evaluationSchemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Validation", "EvaluationSchema.json");
            evaluationSchema = await File.ReadAllTextAsync(evaluationSchemaPath);

            evaluationPrompt = $@"
            You are an AI assistant evaluating the correctness of answers. The 'correctness metric' is a measure of if the generated answer to a given User Question is correct based on the ground truth answer. 
            You will be given the generated answer and the ground truth answer.
            
            You need to compare the generated answer against the ground truth answer and score the answer between one to five using the following rating scale:
            One: The answer is incorrect
            Three: The answer is partially correct, but could be missing some key context or nuance that makes it potentially misleading or incomplete compared to the ground truth answer provided.
            Five: The answer is correct and complete based on the ground truth answer provided.

            You must also provide your reasoning as to why the rating you selected was given.

            The rating value should always be either 1, 3, or 5.

            You will add your thoughts, rating, and ground truth answer into the thoughts JSON and return the JSON as the response along with the ground truth answer.

            User Question: {validationRequest.question_and_answer[0].question}            
            Ground truth answer: {validationRequest.question_and_answer[0].answer}
            Generated answer: {validationRequest.question_and_answer[0].llmResponse}";
        }

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

        if (validationRequest.isProductionEvaluation)
        {
            var evaluationResponse = JsonSerializer.Deserialize<ProductionEvaluation>(chatUpdates.Value.Content[0].Text);

            evaluationResponse = new ProductionEvaluation
            {
                UserQuestion = evaluationResponse!.UserQuestion,
                GeneratedAnswer = evaluationResponse.GeneratedAnswer,
                Rating = evaluationResponse.Rating,
                Thoughts = evaluationResponse.Thoughts,
                KnowledgeBaseDocument = evaluationResponse.KnowledgeBaseDocument
            };

            validationResponse.ProductionEvaluation = evaluationResponse;
        }
        else
        {
            var evaluationResponse = JsonSerializer.Deserialize<Evaluation>(chatUpdates.Value.Content[0].Text);

            evaluationResponse = new Evaluation
            {
                UserQuestion = evaluationResponse!.UserQuestion,
                GeneratedAnswer = evaluationResponse.GeneratedAnswer,
                Rating = evaluationResponse.Rating,
                Thoughts = evaluationResponse.Thoughts,
                GroundTruthAnswer = evaluationResponse.GroundTruthAnswer
            };

            validationResponse.Evaluation = evaluationResponse;
        }

       return validationResponse;
    }
}