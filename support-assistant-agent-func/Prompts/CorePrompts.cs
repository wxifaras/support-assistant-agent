using support_assistant_agent_func.Models;
using System.Text;

namespace support_assistant_agent_func.Prompts;

public static class CorePrompts
{
    public static string GetSystemPrompt() => string.Empty;
    
    public static string GetSummaryPrompt(List<Comment> comments)
    {
        var promptBuilder = new StringBuilder("Given the following comments, create a summary by combining all the fields into a single coherent paragraph.\n\n");

        foreach (var comment in comments)
        {
            promptBuilder.AppendLine($"Comment Text: {comment.comment_text}");
            promptBuilder.AppendLine($"Commented By: {comment.commented_by}");
            promptBuilder.AppendLine($"Commented Date: {comment.commented_date:yyyy-MM-dd}");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("Provide the summary.");

        return promptBuilder.ToString();
    }
}