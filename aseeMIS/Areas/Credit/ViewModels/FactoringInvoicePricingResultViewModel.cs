namespace aseeMIS.Areas.Credit.ViewModels
{
    public class FactoringInvoicePricingResultViewModel
    {
        public decimal FactoringNominalInterestRate { get; set; }
        public decimal FactoringCommissionFixedAmount { get; set; }
        public decimal AdvanceRatePercent { get; set; }
        public decimal RetentionRatePercent { get; set; }
        public decimal DilutionRatePercent { get; set; }
        public decimal DilutionAmount { get; set; }
        public decimal EligibleAmount { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal AdvanceAmount { get; set; }
        public decimal RetentionAmount { get; set; }
        public decimal DiscountRatePercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal NetDisbursementAmount { get; set; }
        public decimal AmountFrom { get; set; }
        public decimal AmountTo { get; set; }
        public int DaysOutstanding { get; set; }
        public int TermMonths { get; set; }
    }
}
