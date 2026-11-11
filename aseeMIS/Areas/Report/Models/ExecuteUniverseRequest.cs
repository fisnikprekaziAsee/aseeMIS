namespace aseeMIS.Areas.Report.Models
{
    public class ExecuteUniverseRequest
    {
        public int UniverseId { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }
}
