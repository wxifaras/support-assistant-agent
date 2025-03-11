namespace support_assistant_agent_func.Models;

public record Knowledgebase
{
    public string problem_id { get; set; }
    public string title { get; set; }
    public string description { get; set; }
    public string status { get; set; }
    public string priority { get; set; }
    public string impact { get; set; }
    public string category { get; set; }
    public DateTime reported_date { get; set; }
    public DateTime? resolved_date { get; set; }
    public string assigned_to { get; set; }
    public string reported_by { get; set; }
    public string root_cause { get; set; }
    public string workaround { get; set; }
    public string resolution { get; set; }
    public List<string> related_incidents { get; set; }
    public List<string> Scope { get; set; }
    public List<Attachment> attachments { get; set; }
    public List<Comment> comments { get; set; }
    public string Summary { get; set; }
}