using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace support_assistant_agent_func.Models;

public class ProductionEvaluation
{
    public string UserQuestion { get; set; }
    public string GeneratedAnswer { get; set; }
    public int Rating { get; set; }
    public string Thoughts { get; set; }
}
