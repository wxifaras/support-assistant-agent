namespace support_assistant_agent_func.Models;

public record Comment
{
    public string comment_id { get; set; }
    public string comment_text { get; set; }
    public string commented_by { get; set; }
    public DateTime commented_date { get; set; }
}