using dsCredit.Core.ViewModels;

namespace aseeMIS.Areas.Credit.ViewModels
{
    public class CreditCaseAmortPlanPreviewViewModel
    {
        public long CreditCaseId { get; set; }
        public string? CaseNumber { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? CurrencyCode { get; set; }
        public string? CreditStatusCode { get; set; }
        public string? StageCode { get; set; }
        public decimal? ApprovedEir { get; set; }
        public CreditCaseAmortPlanVersionViewModel? Version { get; set; }
        public List<CreditCaseAmortPlanInstallmentViewModel> Installments { get; set; } = new();
    }
}
