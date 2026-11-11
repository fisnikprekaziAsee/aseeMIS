using dsMIS.Core.Attributes;

namespace aseeMIS.Areas.Common.ViewModels
{
    public class AdditionalFormFieldViewModel
    {
        public int AdditionalFieldId { get; set; }
        public long? ValueId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int FieldOrder { get; set; }
        public string FieldTypeCode { get; set; } = string.Empty;
        public string? HelpText { get; set; }
        public string? EnumJson { get; set; }
        public decimal? MinValueDecimal { get; set; }
        public decimal? MaxValueDecimal { get; set; }
        public int? ValueMinLength { get; set; }
        public int? ValueMaxLength { get; set; }
        public string? RegexPattern { get; set; }
        public string? ForeignKeyPresetName { get; set; }
        public byte? ViewRowNo { get; set; }
        public byte? ViewColumnWidth { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsActive { get; set; }
        public string? ValueText { get; set; }
        public ExtendedForeignKeyAttribute? ForeignKeyAttribute { get; set; }
        public IEnumerable<object>? ForeignKeyRows { get; set; }
        public IReadOnlyList<KeyValuePair<string, string>> EnumOptions { get; set; } = Array.Empty<KeyValuePair<string, string>>();
    }
}
