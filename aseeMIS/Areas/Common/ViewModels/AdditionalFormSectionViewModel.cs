namespace aseeMIS.Areas.Common.ViewModels
{
    public class AdditionalFormSectionViewModel
    {
        public int FormId { get; set; }
        public string FormSessionId { get; set; } = string.Empty;
        public string LayoutCode { get; set; } = string.Empty;
        public string LayoutTitle { get; set; } = string.Empty;
        public string? LayoutDescription { get; set; }
        public int? RowWidth { get; set; }
        public string? IconClass { get; set; }
        public IReadOnlyList<AdditionalFormFieldViewModel> Fields { get; set; } = Array.Empty<AdditionalFormFieldViewModel>();
    }
}
