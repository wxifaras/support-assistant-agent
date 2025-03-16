using Microsoft.SemanticKernel.ChatCompletion;

namespace support_assistant_agent_func.Extensions;

public static class ChatHistoryExtensions
{
    public static void AddUniqueMessage(this ChatHistory chatHistory, AuthorRole role, string content)
    {
        var isDuplicate = chatHistory.Any(m => m.Content == content.Trim() && m.Role == role);

        if (!isDuplicate)
        {
            chatHistory.AddMessage(role, content.Trim());
        }
    }
}