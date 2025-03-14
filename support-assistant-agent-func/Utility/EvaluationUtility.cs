using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using support_assistant_agent_func.Models;
using System.Text.Json;


namespace support_assistant_agent_func.Utility
{
    public interface IEvaluationUtility
    {
      Task<Object> EvaluateSearchResult(string searchText, string pId, string llmResult);
    }

    class EvaluationUtility: IEvaluationUtility
    {

        private readonly AzureOpenAIClient _azureOpenAIClient;
        private readonly IOptions<AzureOpenAIOptions> _azureOpenAIOptions;
        private readonly string _azureOpenAIGPTDeployment;

        public EvaluationUtility(AzureOpenAIClient azureOpenAIClient, IOptions<AzureOpenAIOptions> azureOpenAIOptions)
        {
            _azureOpenAIClient = azureOpenAIClient;
            _azureOpenAIGPTDeployment = azureOpenAIOptions.Value.AzureOpenAIDeployment ?? throw new ArgumentNullException(nameof(azureOpenAIOptions.Value.AzureOpenAIDeployment));
        }

        public async Task<Object> EvaluateSearchResult(string searchText, string pId, string llmResult)
        {
            // Load Ground Truth Data
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string groundTruthPath = Path.Combine(baseDirectory, "Utility", "GroundTruthDoc.json");
            string groundTruthQAContent = File.ReadAllText(groundTruthPath);

            string groundTruthSchemaPath = Path.Combine(baseDirectory, "Utility", "GroundTruthSchema.json");
            string groundTruthSchema = File.ReadAllText(groundTruthSchemaPath);

            var evaluationSchemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Utility", "EvaluationSchema.json");
            var evaluationSchema = File.ReadAllText(evaluationSchemaPath);

            var evaluationPrompt = $@"
              You are an AI assistant evaluating the correctness of answers.

             Below is the ground truth file with the following schema:
             {groundTruthSchema}
             and the ground truth content:
             {groundTruthQAContent}
 
             Please scan the ground truth content for the corresponding problem_id: {pId} and check the answer with the 
             generated answer from the model which is Response: {llmResult}

              The rating value should always be either 1, 3, or 5.
              One: The answer is incorrect
              Three: The answer is partially correct, but could be missing some key context or nuance that makes it potentially misleading or incomplete compared to the context provided.
              Five: The answer is correct and complete based on the context provided.

              User Query: {searchText}
          
              Return a JSON object with the following scores:
              -accuracy_score: Measures how factually correct the response is (1, 3, or 5).
              -completeness_score: Measures if the response covers all relevant points(1, 3, or 5).
              - relevance_score: Measures how well the response aligns with the user query(1, 3, or 5).
              -thoughtprocess: You will add your thoughts and rating for each accuracy_score,completeness_score and relevance_score into the thoughtprocess JSON and return the JSON as the response.
              JSON should be well formed.

             The rating value should always be either 1, 3, or 5.
             ";

            var client = _azureOpenAIClient.GetChatClient(_azureOpenAIGPTDeployment);
            var chat = new List<ChatMessage>()
            {
              new SystemChatMessage(evaluationPrompt)
            };

            //Create chat completion options
            var chatUpdates = client.CompleteChat(
                chat,
                new ChatCompletionOptions()
                {
                    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("Eval", BinaryData.FromString(evaluationSchema))
                });

            // Deserialize the JSON response into the Evaluation object
            var evaluationResponse = JsonSerializer.Deserialize<Evaluation>(chatUpdates.Value.Content[0].Text);

            evaluationResponse = new Evaluation
            {
                ProblemId = evaluationResponse.ProblemId,
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

           return evaluationResponse;
        }
    }
}
