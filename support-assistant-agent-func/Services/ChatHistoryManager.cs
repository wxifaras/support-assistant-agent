using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Concurrent;

namespace support_assistant_agent_func.Services;

public class ChatHistoryManager : IChatHistoryManager
{
    private readonly ConcurrentDictionary<string, (ChatHistory History, DateTime LastAccessed)> _chatHistories = new();
    private readonly string _systemMessage;
    private readonly TimeSpan _expirationTime = TimeSpan.FromHours(1); // Adjust as needed

    public ChatHistoryManager(string systemMessage)
    {
        _systemMessage = systemMessage;
    }

    public async Task<ChatHistory> GetOrCreateChatHistoryAsync(string sessionId)
    {
        return await Task.Run(() =>
        {
            return _chatHistories.AddOrUpdate(
                sessionId,
                _ => (CreateNewChatHistory(), DateTime.UtcNow),
                (_, old) => (old.History, DateTime.UtcNow)
            ).History;
        });
    }

    private ChatHistory CreateNewChatHistory()
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(_systemMessage);
        return chatHistory;
    }

    public void CleanupOldHistories()
    {
        var cutoff = DateTime.UtcNow - _expirationTime;
        foreach (var key in _chatHistories.Keys)
        {
            if (_chatHistories.TryGetValue(key, out var value) && value.LastAccessed < cutoff)
            {
                _chatHistories.TryRemove(key, out _);
            }
        }
    }

    public bool ClearChatHistory(string sessionId)
    {
        return _chatHistories.TryRemove(sessionId, out _);
    }

    Task IChatHistoryManager.SaveChatHistoryAsync(string sessionId, ChatHistory chatHistory)
    {
        throw new NotImplementedException();
    }
}