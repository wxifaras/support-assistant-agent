using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace support_assistant_agent_func.Models
{
    public record EvaluationScores
    {
        public double AccuracyScore { get; set; }
        public double CompletenessScore { get; set; }
        public double RelevanceScore { get; set; }
    }
}
