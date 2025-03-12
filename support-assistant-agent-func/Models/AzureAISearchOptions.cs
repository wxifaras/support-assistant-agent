using System.ComponentModel.DataAnnotations;

namespace support_assistant_agent_func.Models;

public class AzureAISearchOptions
{
    public const string AzureAISearch = "AzureAISearchOptions";

    [Required]
    public string IndexName { get; set; }

    [Required]
    public string SearchServiceEndpoint { get; set; }

    [Required]
    public string SearchAdminKey { get; set; }

    [Required]
    public string RerankerThreshold { get; set; }
}