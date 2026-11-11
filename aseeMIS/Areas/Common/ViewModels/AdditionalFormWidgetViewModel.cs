namespace aseeMIS.Areas.Common.ViewModels
{
    public class AdditionalFormWidgetViewModel
    {
        public int FormId { get; set; }
        public string FormSessionId { get; set; } = string.Empty;
        public string ParentFormSessionId { get; set; } = string.Empty;
        public string? TargetId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string FlowForeignKeyAttributeName { get; set; } = string.Empty;
        public string ForeignKeyPropertyName { get; set; } = string.Empty;
        public IReadOnlyList<AdditionalFormSectionViewModel> Sections { get; set; } = Array.Empty<AdditionalFormSectionViewModel>();
        public string? DebugMessage { get; set; }
    }
}
