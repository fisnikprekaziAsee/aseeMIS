using dsCreditEngine.Engine;
using dsCreditEngine.Enums;
using dsCreditEngine.Models;
using aseeMIS.Areas.Credit.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Credit.Controllers
{
    [Area("Credit")]
    public class CreditEngineController : Controller
    {
        /*
        // Standalone engine lab. Not part of the current Credit Hub/Loan360 flow.
        [HttpGet]
        public IActionResult AmortizationLab()
        {
            var model = new CreditEngineAmortizationLabViewModel();
            model.Normalize();
            ApplyProjectedEndDate(model);
            return View("~/Areas/Credit/Views/Loan/CreditEngineAmortizationLab.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AmortizationLab(CreditEngineAmortizationLabViewModel model)
        {
            model.Normalize();

            try
            {
                var parameters = BuildParameters(model);
                ApplyProjectedEndDate(model, parameters);
                model.Result = CreditAmortizationEngine.Generate(parameters);
                model.SuccessMessage = $"Engine generated {model.Result.Rows.Count} installment row(s) successfully.";
            }
            catch (Exception ex)
            {
                model.ErrorMessage = ex.Message;
            }

            return View("~/Areas/Credit/Views/Loan/CreditEngineAmortizationLab.cshtml", model);
        }

        private static CreditLoanEngineParameters BuildParameters(CreditEngineAmortizationLabViewModel model)
        {
            var parameters = new CreditLoanEngineParameters
            {
                Principal = model.Principal,
                BaseAnnualRate = model.BaseAnnualRate,
                StartDate = model.StartDate.Date,
                MaturityMonths = model.MaturityMonths,
                NextInstallmentDate = model.NextInstallmentDate?.Date,
                FrequencyCode = model.FrequencyCode,
                DueDayPolicy = model.DueDayPolicy,
                FixedDayOfMonth = model.FixedDayOfMonth,
                UseEndOfMonth = model.UseEndOfMonth,
                DayCountConvention = model.DayCountConvention,
                PrincipalCalculationMethod = model.PrincipalCalculationMethod,
                FixedInstallmentAmount = model.FixedInstallmentAmount,
                FixedPrincipalAmount = model.FixedPrincipalAmount,
                GraceType = model.GraceType,
                GraceMonths = model.GraceMonths,
                GraceUntilDate = model.GraceUntilDate?.Date,
                AccrueInterestDuringFullGrace = model.AccrueInterestDuringFullGrace,
                CapitalizeDuringFullGrace = model.CapitalizeDuringFullGrace,
                RateType = model.UseIndexedRate ? CreditRateType.Indexed : CreditRateType.Fixed,
                IndexCode = model.IndexCode,
                IndexRatePercent = model.IndexRatePercent,
                CurrencyDecimals = model.CurrencyDecimals,
                RoundingMode = model.RoundingMode,
                BalloonPolicy = model.AddFinalBalloonAtMaturity
                    ? CreditBalloonPolicy.AtMaturityAll
                    : CreditBalloonPolicy.None
            };

            if (model.DisbursementFeePercent > 0m || model.MinimumFeeAmount > 0m)
            {
                parameters.DisbursementFees.Add(new CreditFeeRule
                {
                    Name = "Disbursement Fee",
                    FeePercent = model.DisbursementFeePercent,
                    MinimumAmount = model.MinimumFeeAmount
                });
            }

            if (model.UseIndexedRate && !string.IsNullOrWhiteSpace(model.IndexCode))
            {
                parameters.IndexRateHistory.Add(new CreditIndexedRate
                {
                    IndexCode = model.IndexCode,
                    EffectiveDate = model.StartDate.Date,
                    RatePercent = model.IndexRatePercent
                });
            }

            return parameters;
        }

        private static void ApplyProjectedEndDate(CreditEngineAmortizationLabViewModel model)
        {
            var parameters = BuildParameters(model);
            ApplyProjectedEndDate(model, parameters);
        }

        private static void ApplyProjectedEndDate(CreditEngineAmortizationLabViewModel model, CreditLoanEngineParameters parameters)
        {
            var scheduleDates = CreditScheduleBuilder.Build(parameters);
            model.EndDate = scheduleDates.LastOrDefault();
        }
        */
    }
}
