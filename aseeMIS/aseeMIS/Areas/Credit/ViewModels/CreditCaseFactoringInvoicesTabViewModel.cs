using dsCredit.Core.ViewModels;

namespace aseeMIS.Areas.Credit.ViewModels
{
    public class CreditCaseFactoringInvoicesTabViewModel
    {
        public long? CreditCaseId { get; set; }
        public string? FormSessionId { get; set; }
        public int FormId { get; set; }
        public string? ProductCode { get; set; }
        public string? CurrentStatusCode { get; set; }
        public string CurrencyCode { get; set; } = "EUR";
        public CreditCaseFactoringInvoiceViewModel Invoice { get; set; } = new();
        public List<CreditCaseFactoringInvoiceViewModel> Invoices { get; set; } = new();
        public IEnumerable<object> FactoringTypes { get; set; } = Enumerable.Empty<object>();
        public IEnumerable<object> RecoursePolicies { get; set; } = Enumerable.Empty<object>();
        public IEnumerable<object> CaseStatuses { get; set; } = Enumerable.Empty<object>();
        public IEnumerable<object> CaseWorkflowSteps { get; set; } = Enumerable.Empty<object>();
        public IEnumerable<object> RiskBearers { get; set; } = Enumerable.Empty<object>();
        public IEnumerable<object> FactoringAgreements { get; set; } = Enumerable.Empty<object>();
        public IEnumerable<object> FactoringParties { get; set; } = Enumerable.Empty<object>();
        public IEnumerable<object> Banks { get; set; } = Enumerable.Empty<object>();
    }
}
