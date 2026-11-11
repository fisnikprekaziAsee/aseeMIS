namespace aseeMIS.Areas.Common.ViewModels
{
    public class SearchAutocompleteOptions
    {
        public string SearchUrl { get; set; } = default!;
        public string Id { get; set; } = "";
        public string Placeholder { get; set; } = "Type at least 3 characters…";
        public int MinLength { get; set; } = 3;
        public int DebounceDelay { get; set; } = 400;
        public int SuggestionCount { get; set; } = 10;

        // Field mapping for the control
        public string ValueField { get; set; } = "value";
        public string TextField { get; set; } = "text";

        // (Optional) expose selected value/text if you need two-way binding later
        public string? SelectedValue { get; set; }
        public string? SelectedText { get; set; }

       
        // REQUIRED – must be unique per control
        public string FilteringFn { get; set; } = default!;
        public string? OnSelectFn { get; set; }

        /// <summary>Syncfusion float label: Always, Auto, Never.</summary>
        public string FloatLabelType { get; set; } = "Always";
    }
}
