using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace support_assistant_agent_func.Models
{
    public class Evaluation
    {
        public string ProblemId { get; set; }
        public string UserQuestion { get; set; }
        public string GeneratedAnswer { get; set; }    
        public int AccuracyScore { get; set; }
        public int CompletenessScore { get; set; }
        public int RelevanceScore { get; set; }
        public ThoughtProcess ThoughtProcess { get; set; }
        public string GroundTruthAnswer { get; set; }
    }
    public class ThoughtProcess
    {
        public string AccuracyScore { get; set; }
        public string CompletenessScore { get; set; }
        public string RelevanceScore { get; set; }
    }
}
