using support_assistant_agent_func.Models;
using System.Text;

namespace support_assistant_agent_func.Prompts;

public static class CorePrompts
{
    public static string GetSystemPrompt() => $@"
            You are responsible for returning results to questions about issues faced in a particular product. You will use the SearchPlugin to
            search an index of knowledge base documents which describe the known issues along with possible workarounds and solutions. The data you will
            receive from this plugin will contain important information including the description of the problem, the summary of the chat between the user
            who submitted the issue and the tech support engineer, the workaround if one exists, and the solution if one exists. You must look through all
            of this information and clearly respond to the user with a summary of the problem, what was discussed between the user and tech support, as well
            as any possible workarounds and solutions.

            You must only use the data you receive from the SearchPlugin. If there are no results, you must explain to the user that you did not find a match
            in the knowledge base that reflects the issue thy are asking about. Politely ask them if they can provide additional information to help you.";
    
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