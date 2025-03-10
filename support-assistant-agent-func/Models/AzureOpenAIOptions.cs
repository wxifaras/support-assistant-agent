using System.ComponentModel.DataAnnotations;

namespace support_assistant_agent_func.Models;

public class AzureOpenAIOptions
{
    public const string AzureOpenAI = "AzureOpenAIOptions";

    [Required]
    public string AzureOpenAIDeployment { get; set; }

    [Required]
    public string AzureOpenAIEndPoint { get; set; }

    [Required]
    public string AzureOpenAIKey { get; set; }

    [Required]
    public string AzureOpenAIEmbeddingDeployment { get; set; }

    [Required]
    public string AzureOpenAIEmbeddingDimensions { get; set; }
}