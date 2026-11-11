using dsCreditPriceList.Core.DTOs;

namespace aseeMIS.Areas.Credit.ViewModels
{
    public class FactoringInvoicePricingContextViewModel
    {
        public string ProductCode { get; set; } = string.Empty;
        public int? PriceListId { get; set; }
        public decimal AdvanceRatePercent { get; set; }
        public decimal RetentionRatePercent { get; set; }
        public decimal DilutionRatePercent { get; set; }
        public List<CreditPriceListItemFactoringPricingDto> PriceListItems { get; set; } = new();
        public string? Message { get; set; }
        public int CaseOwnerBankPartyId { get; set; }
        public bool CaseOwnerIsIndividual { get; set; }
    }
}
