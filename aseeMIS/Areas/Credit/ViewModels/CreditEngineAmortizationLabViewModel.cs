using dsCreditEngine.Enums;
using dsCreditEngine.Models;
using System.ComponentModel.DataAnnotations;

namespace aseeMIS.Areas.Credit.ViewModels
{
    public class CreditEngineAmortizationLabViewModel
    {
        [Display(Name = "Principal Amount")]
        public decimal Principal { get; set; } = 10000m;

        [Display(Name = "Nominal Annual Interest Rate (%)")]
        public decimal BaseAnnualRate { get; set; } = 12m;

        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "Term (months)")]
        public int MaturityMonths { get; set; } = 12;

        [Display(Name = "Projected End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Next Installment Date")]
        [DataType(DataType.Date)]
        public DateTime? NextInstallmentDate { get; set; } = DateTime.Today.AddMonths(1);

        [Display(Name = "Frequency")]
        public CreditFrequencyCode FrequencyCode { get; set; } = CreditFrequencyCode.Monthly;

        [Display(Name = "Due Day Policy")]
        public CreditDueDayPolicy DueDayPolicy { get; set; } = CreditDueDayPolicy.FollowFirstInstallmentDate;

        [Display(Name = "Fixed Day Of Month")]
        public int? FixedDayOfMonth { get; set; } = 28;

        [Display(Name = "Use End Of Month")]
        public bool UseEndOfMonth { get; set; }

        [Display(Name = "Day Count Convention")]
        public CreditDayCountConvention DayCountConvention { get; set; } = CreditDayCountConvention.Actual360;

        [Display(Name = "Principal Calculation Method")]
        public CreditPrincipalCalculationMethod PrincipalCalculationMethod { get; set; } = CreditPrincipalCalculationMethod.Annuity;

        [Display(Name = "Fixed Installment Amount")]
        public decimal? FixedInstallmentAmount { get; set; }

        [Display(Name = "Fixed Principal Amount")]
        public decimal? FixedPrincipalAmount { get; set; }

        [Display(Name = "Grace Type")]
        public CreditGraceType GraceType { get; set; } = CreditGraceType.None;

        [Display(Name = "Grace Months")]
        public int GraceMonths { get; set; }

        [Display(Name = "Grace Until Date Override")]
        [DataType(DataType.Date)]
        public DateTime? GraceUntilDate { get; set; }

        [Display(Name = "Accrue Interest During Full Grace")]
        public bool AccrueInterestDuringFullGrace { get; set; } = true;

        [Display(Name = "Capitalize During Full Grace")]
        public bool CapitalizeDuringFullGrace { get; set; }

        [Display(Name = "Use Indexed Rate")]
        public bool UseIndexedRate { get; set; }

        [Display(Name = "Index Code")]
        public string IndexCode { get; set; } = "EURIBOR_3M";

        [Display(Name = "Current Index Rate (%)")]
        public decimal IndexRatePercent { get; set; }

        [Display(Name = "Disbursement Fee (%)")]
        public decimal DisbursementFeePercent { get; set; } = 1.5m;

        [Display(Name = "Minimum Fee Amount")]
        public decimal MinimumFeeAmount { get; set; } = 10m;

        [Display(Name = "Add Final Balloon At Maturity")]
        public bool AddFinalBalloonAtMaturity { get; set; }

        public int CurrencyDecimals { get; set; } = 2;
        public CreditRoundingMode RoundingMode { get; set; } = CreditRoundingMode.AwayFromZero;

        public CreditAmortizationResult? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public decimal AppliedDisbursementFeeAmount { get; set; }
        public decimal AppliedDisbursementFeePercent { get; set; }
        public bool MinimumFeeApplied { get; set; }

        public void Normalize()
        {
            if (MaturityMonths <= 0)
            {
                MaturityMonths = 1;
            }

            StartDate = StartDate.Date;
            EndDate = StartDate.AddMonths(MaturityMonths);

            if (!NextInstallmentDate.HasValue || NextInstallmentDate.Value <= StartDate)
            {
                NextInstallmentDate = StartDate.AddMonths(1);
            }

            if (GraceMonths < 0)
            {
                GraceMonths = 0;
            }

            var fee = MinimumFeeAmount > 0m
                ? Math.Max(MinimumFeeAmount, Principal * DisbursementFeePercent / 100m)
                : Principal * DisbursementFeePercent / 100m;

            AppliedDisbursementFeeAmount = Math.Round(fee, 2, MidpointRounding.AwayFromZero);
            MinimumFeeApplied = MinimumFeeAmount > 0m && AppliedDisbursementFeeAmount == MinimumFeeAmount && DisbursementFeePercent > 0m;
            AppliedDisbursementFeePercent = Principal > 0m
                ? Math.Round(AppliedDisbursementFeeAmount * 100m / Principal, 4, MidpointRounding.AwayFromZero)
                : 0m;
        }
    }
}
