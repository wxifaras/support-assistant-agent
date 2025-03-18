using Microsoft.SemanticKernel.ChatCompletion;

namespace support_assistant_agent_func.Services;

public interface IChatHistoryManager
{
    Task<ChatHistory> GetOrCreateChatHistoryAsync(string sessionId);
    Task SaveChatHistoryAsync(string sessionId, ChatHistory chatHistory);
    void CleanupOldHistories();
    bool ClearChatHistory(string sessionId);
}