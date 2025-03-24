namespace support_assistant_agent_func.Models;

public class ValidationRequest
{
    public string problem_id { get; set; }
    public List<QuestionAndAnswer> question_and_answer { get; set; }
    public string SearchText { get; set; }
    public List<string> scope { get; set; }
    public Evaluation Evaluation { get; set; }
    public ProductionEvaluation ProductionEvaluation { get; set; }
    public bool isProductionEvaluation { get; set; }
}

public class QuestionAndAnswer
{
    public string question { get; set; }
    public string answer { get; set; }
    public string llmResponse { get; set; }
}