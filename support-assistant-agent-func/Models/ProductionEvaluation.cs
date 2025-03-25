﻿namespace support_assistant_agent_func.Models;

public class ProductionEvaluation
{
    public string UserQuestion { get; set; }
    public string GeneratedAnswer { get; set; }
    public int Rating { get; set; }
    public string Thoughts { get; set; }
    public string KnowledgeBaseDocument { get; set; }
}