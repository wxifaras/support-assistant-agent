using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using support_assistant_agent_func.Models;
using System.Text.Json;

namespace support_assistant_agent_func.Services;

public class ChatHistoryItem
{
    public required string id { get; set; }
    public required string SessionId { get; set; } // partition key
    public required string ChatHistory { get; set; }
    public DateTime LastAccessed { get; set; }
}

public class SKCosmosDbChatManager : IChatHistoryManager
{
    private readonly Container _chatContainer;
    private readonly string _systemMessage;

    public SKCosmosDbChatManager(IOptions<CosmosDbOptions> options, string systemMessage)
    {
        CosmosClient cosmosClient = new(
           accountEndpoint: options.Value.AccountUri,
           tokenCredential: new DefaultAzureCredential(
               new DefaultAzureCredentialOptions
               {
                   TenantId = options.Value.TenantId,
                   ExcludeEnvironmentCredential = true
               })
       );

      _chatContainer = cosmosClient.GetContainer(options.Value.DatabaseName, options.Value.ContainerName);
      _systemMessage = systemMessage;
    }

    public async Task<ChatHistory> GetOrCreateChatHistoryAsync(string sessionId)
    {
        var partitionKey = GetPK(sessionId);
        ChatHistory chatHistory;

        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.SessionId = @sessionId").WithParameter("@sessionId", sessionId);

            using FeedIterator<ChatHistoryItem> feedIterator = _chatContainer.GetItemQueryIterator<ChatHistoryItem>(
                query,
                requestOptions: new QueryRequestOptions { PartitionKey = partitionKey });

            if (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                if (response.Count > 0)
                {
                    var existingItem = response.FirstOrDefault() ?? throw new InvalidOperationException($"No existing chat history found sessionId {sessionId}");
                    existingItem.LastAccessed = DateTime.UtcNow;
                    await _chatContainer.ReplaceItemAsync(existingItem, existingItem.id, partitionKey);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    chatHistory = JsonSerializer.Deserialize<ChatHistory>(existingItem.ChatHistory, options) ?? [];
                    return chatHistory;
                }
            }

            // If no item found, create a new one
            chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(_systemMessage);

            var jsonChatHistory = JsonSerializer.Serialize(chatHistory, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var newItem = new ChatHistoryItem
            {
                id = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                ChatHistory = jsonChatHistory,
                LastAccessed = DateTime.UtcNow
            };

            await _chatContainer.CreateItemAsync(newItem, partitionKey);
            return chatHistory;
        }
        catch (CosmosException ex)
        {
            // Handle any other Cosmos DB exceptions here
            throw;
        }
    }

    public async Task SaveChatHistoryAsync(string sessionId, ChatHistory chatHistory)
    {
        var partitionKey = GetPK(sessionId);

        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.SessionId = @sessionId").WithParameter("@sessionId", sessionId);

            using FeedIterator<ChatHistoryItem> feedIterator = _chatContainer.GetItemQueryIterator<ChatHistoryItem>(
                query,
                requestOptions: new QueryRequestOptions { PartitionKey = partitionKey });

            ChatHistoryItem? existingItem = null;
            if (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();

                if (response.Count > 0)
                {
                    existingItem = response.FirstOrDefault() ?? throw new InvalidOperationException($"No existing chat history found sessionId {sessionId}");
                }
            }

            var jsonChatHistory = JsonSerializer.Serialize(chatHistory, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            existingItem!.ChatHistory = jsonChatHistory;
            existingItem.LastAccessed = DateTime.UtcNow;

            //await _chatContainer.UpsertItemAsync(existingItem, partitionKey);
            await _chatContainer.ReplaceItemAsync(existingItem, existingItem.id, partitionKey);
        }
        catch (CosmosException ex)
        {
            // Handle any Cosmos DB exceptions here
            throw;
        }
    }

    private static PartitionKey GetPK(string sessionId)
    {
        return new PartitionKeyBuilder()
            .Add(sessionId)
            .Build();
    }

    void IChatHistoryManager.CleanupOldHistories()
    {
        throw new NotImplementedException();
    }

    bool IChatHistoryManager.ClearChatHistory(string sessionId)
    {
        throw new NotImplementedException();
    }
}