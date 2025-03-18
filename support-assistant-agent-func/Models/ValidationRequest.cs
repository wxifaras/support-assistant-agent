using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace support_assistant_agent_func.Models;

public class ValidationRequest
{
    public string problem_id { get; set; }
    public List<QuestionAndAnswer> question_and_answer { get; set; }
    public string SearchText { get; set; }
    public List<string> scope { get; set; }
    public Evaluation Evaluation { get; set; }

}
public class QuestionAndAnswer
{
    public string question { get; set; }
    public string answer { get; set; }
    public string llmResponse { get; set; }
}
