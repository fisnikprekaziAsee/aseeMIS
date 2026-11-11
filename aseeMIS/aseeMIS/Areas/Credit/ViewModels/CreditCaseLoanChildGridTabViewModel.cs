namespace aseeMIS.Areas.Credit.ViewModels
{
    public class CreditCaseLoanChildGridTabViewModel
    {
        public long? CreditCaseId { get; set; }
        public string? FormSessionId { get; set; }
        public int FormId { get; set; }
        public string ForeignAttributeName { get; set; } = "CreditCaseId";
        public string PrimaryAttributeName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SaveFirstMessage { get; set; } = "Save the loan application first to manage this tab.";
    }
}
