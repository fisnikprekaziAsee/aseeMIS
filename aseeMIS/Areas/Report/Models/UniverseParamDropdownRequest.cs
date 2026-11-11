namespace aseeMIS.Areas.Report.Models
{
    public class UniverseParamDropdownRequest
    {
        public string ProjectAcronym { get; set; } = null!;
        public string ModelName { get; set; } = null!;
        public string? ServiceName { get; set; }
        public string MethodName { get; set; } = null!;
        public string? MethodParams { get; set; }
    }
}
