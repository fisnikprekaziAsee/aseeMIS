using dsLoanEngine.Core.Enums;
using dsLoanEngine.Core.Models;
using dsLoanEngine.Core.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace aseeMIS.Areas.Credit.ViewModels
{
    /// <summary>
    /// Input view model for the Credit amortization plan generator tab.
    /// </summary>
    public class CreditCaseAmortPlanInputViewModel
    {
        [Display(Name = "Credit Case Number")]
        public string? CreditCaseNumber { get; set; }

        public long? CreditCaseId { get; set; }

        [Display(Name = "Client")]
        public int? SelectedBankPartyId { get; set; }
        public List<KeyValuePair<string, string>> BankPartyOptions { get; set; } = new();

        [Display(Name = "Credit Case")]
        public long? SelectedCreditCaseId { get; set; }
        public List<KeyValuePair<string, string>> CreditCaseOptions { get; set; } = new();

        public string? SelectedProductCode { get; set; }
        public List<KeyValuePair<string, string>> ProductOptions { get; set; } = new();

        public string? SelectedTemplateCode { get; set; }
        public List<KeyValuePair<string, string>> TemplateOptions { get; set; } = new();
        public string? TemplateName { get; set; }
        public string? TemplateDescription { get; set; }
        public string? TemplateAmortizationMethodCode { get; set; }
        public string? TemplatePaymentMethodCode { get; set; }
        public string? InterestPostingFrequencyCode { get; set; }
        public bool AllowSeasonal { get; set; }
        public string? InstallmentMonthsMask { get; set; }
        public int? GraceMonthsCount { get; set; }
        public string? GraceMonthsMaskPrincipal { get; set; }
        public string? GraceMonthsMaskInterest { get; set; }
        public string? RateTypeCode { get; set; }
        public string? RateResetFrequency { get; set; }
        public int? RateMarginBps { get; set; }
        public string? LastInstallmentAdjustCode { get; set; }

        [Display(Name = "Principal Amount")]
        [Required]
        public decimal Principal { get; set; } = 10000m;

        [Display(Name = "Base Annual Rate (%)")]
        public decimal BaseAnnualRate { get; set; } = 5.0m;

        [Display(Name = "Term (months)")]
        [Required]
        public int TermMonths { get; set; } = 12;

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(12);

        [DataType(DataType.Date)]
        [Display(Name = "Next Installment Date")]
        public DateTime? NextInstallmentDate { get; set; }

        [Display(Name = "Use Indexed Rate (e.g., EURIBOR)")]
        public bool UseIndexedRate { get; set; } = false;

        [Display(Name = "Index Code")]
        public string IndexCode { get; set; } = "EURIBOR_3M";

        [Display(Name = "Current Index Rate (%)")]
        public decimal IndexRatePercent { get; set; } = 0m;

        public List<DisbursementFeeInput> DisbursementFees { get; set; } = new();

        [Display(Name = "Total Disbursement Fees (%)")]
        public decimal TotalDisbursementFeePercent { get; set; } = 0m;

        [Display(Name = "Day Count Convention")]
        public EnumDayCountCalculationType DayCountMethod { get; set; } = EnumDayCountCalculationType.Actual360;

        [Display(Name = "Principal Calculation Method")]
        public PrincipalCalculationMethod PrincipalCalcMethod { get; set; } = PrincipalCalculationMethod.Annuity;

        [Display(Name = "Fixed Installment Amount")]
        public decimal? FixedInstallmentAmount { get; set; }

        [Display(Name = "Fixed Principal Amount")]
        public decimal? FixedPrincipalAmount { get; set; }
        public int CurrencyDecimals { get; set; } = 2;
        public RoundingMode RoundingMode { get; set; } = RoundingMode.AwayFromZero;

        public FrequencyCode FrequencyCode { get; set; } = FrequencyCode.Monthly;
        public DueDayPolicy DueDayPolicy { get; set; } = DueDayPolicy.EndOfMonth;
        public int FixedDayOfMonth { get; set; } = 28;

        public GraceType GraceType { get; set; } = GraceType.None;
        public DateTime? GraceUntilDate { get; set; }
        public bool AccrueInterestDuringFullGrace { get; set; } = true;
        public bool CapitalizeDuringFullGrace { get; set; } = false;

        public bool AddFinalBalloonAll { get; set; } = false;

        public AdvancedAmortizationResult? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public decimal? ProductAmountMin { get; set; }
        public decimal? ProductAmountMax { get; set; }
    }

    public class DisbursementFeeInput
    {
        public string Name { get; set; } = string.Empty;
        public decimal FeePercent { get; set; }
        public decimal FeeAmount { get; set; }
    }
}
