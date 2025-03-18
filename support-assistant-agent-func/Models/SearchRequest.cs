using System.ComponentModel.DataAnnotations;

namespace support_assistant_agent_func.Models;

public record SearchRequest
{
    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public string Scope { get; set; }

    [Required]
    public string SearchText { get; set; }

    public string EvalRequired { get; set; }

    public string ProblemId { get; set; }
}