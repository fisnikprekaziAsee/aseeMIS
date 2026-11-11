using dsLoanEngine.Core.Enums;
using dsLoanEngine.Core.Models;
using dsLoanEngine.Core.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace aseeMIS.Areas.Credit.ViewModels
{
    public class CreditLoanAmortPlanTabViewModel
    {
        public int? BankPartyId { get; set; }
        public long? CreditCaseId { get; set; }

        [Display(Name = "Credit Case Number")]
        public string? CreditCaseNumber { get; set; }

        public string? ProductCode { get; set; }

        public string? SelectedTemplateCode { get; set; }
        public List<KeyValuePair<string, string>> TemplateOptions { get; set; } = new();

        public string? TemplateName { get; set; }
        public string? TemplateDescription { get; set; }
        public string? TemplateAmortizationMethodCode { get; set; }
        public string? TemplatePaymentMethodCode { get; set; }
        public string? InterestPostingFrequencyCode { get; set; }
        public bool AllowSeasonal { get; set; }
        public string? InstallmentMonthsMask { get; set; }
        public string? GraceMonthsMaskPrincipal { get; set; }
        public string? GraceMonthsMaskInterest { get; set; }
        public string? RateTypeCode { get; set; }
        public string? RateResetFrequency { get; set; }
        public int? RateMarginBps { get; set; }
        public string? LastInstallmentAdjustCode { get; set; }

        [Display(Name = "Principal Amount")]
        public decimal? Principal { get; set; }

        [Display(Name = "Nominal Annual Interest Rate (%)")]
        public decimal? BaseAnnualRate { get; set; }

        [Display(Name = "Term (months)")]
        public int? TermMonths { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Next Installment Date")]
        public DateTime? NextInstallmentDate { get; set; }

        [Display(Name = "Use Indexed Rate")]
        public bool UseIndexedRate { get; set; }

        [Display(Name = "Index Code")]
        public string IndexCode { get; set; } = "EURIBOR_3M";

        [Display(Name = "Current Index Rate (%)")]
        public decimal? IndexRatePercent { get; set; }

        [Display(Name = "Total Disbursement Fee (%)")]
        public decimal? TotalDisbursementFeePercent { get; set; }

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

        [Display(Name = "Grace Months")]
        public int? GraceMonthsCount { get; set; }

        public GraceType GraceType { get; set; } = GraceType.None;

        [DataType(DataType.Date)]
        [Display(Name = "Grace Until Date")]
        public DateTime? GraceUntilDate { get; set; }

        public bool AccrueInterestDuringFullGrace { get; set; } = true;
        public bool CapitalizeDuringFullGrace { get; set; }
        public bool AddFinalBalloonAll { get; set; }

        public AdvancedAmortizationResult? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public decimal? ProductAmountMin { get; set; }
        public decimal? ProductAmountMax { get; set; }

        /// <summary>requested | approved — which CreditCase term set drives generation.</summary>
        public string TermsEditMode { get; set; } = "requested";

        public decimal? DisplayEffectiveInterestRate { get; set; }
        public decimal? DisplayChargeAmount { get; set; }
        public decimal? DisplayPenaltyInterest { get; set; }

        public bool IsApprovedTerms => string.Equals(TermsEditMode, "approved", StringComparison.OrdinalIgnoreCase);

        public bool CanSave => CreditCaseId.HasValue && CreditCaseId.Value > 0 && Result?.Rows.Count > 0;
    }
}
