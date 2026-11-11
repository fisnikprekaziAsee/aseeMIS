namespace aseeMIS.Areas.BankParty.ViewModels
{
    public class BankPartyContextWidgetOptions
    {
        public string WidgetId { get; set; } = "bpCtx";

        public int? SelectedBankPartyId { get; set; }

        public bool? SelectedIsIndividual { get; set; }

        public string LanguageCode { get; set; } = "en";

        public string Title { get; set; } = "Select Client";

        public string Subtitle { get; set; } =
            "Search and select an existing business or individual client to continue.";

        public bool ShowEntityTypeButtons { get; set; } = true;

        public bool DefaultIsIndividual { get; set; } = true;

        public bool CollapseAfterSelection { get; set; } = true;

        public bool ShowEntityFormButton { get; set; }

        public string FormSessionId { get; set; } = string.Empty;
    }
}