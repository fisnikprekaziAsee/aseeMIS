using dsCredit.Core.ViewModels;
using dsLoanEngine.Core.Engine;
using dsLoanEngine.Core.Enums;
using dsLoanEngine.Core.Models;
using dsLoanEngine.Core.ViewModels;
using aseeMIS.Areas.Credit.Services.Interfaces;

namespace aseeMIS.Areas.Credit.Services
{
    /// <summary>
    /// Loan-tab amortization: maps CreditCaseLoanViewModel → dsLoanEngine → view model.
    /// dsCredit.Core persists the result via CreditCaseLoanService.
    /// </summary>
    public class CreditCaseLoanAmortPlanService : ICreditCaseLoanAmortPlanService
    {
        public Task<CreditCaseLoanViewModel> RegenerateAsync(
            CreditCaseLoanViewModel model,
            string? generatedBy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(model);

            var creditCase = model.CreditCase
                ?? throw new InvalidOperationException("Credit case data is required.");

            if (IsDisbursed(creditCase.CreditStatusCode))
            {
                model.CreditCaseAmortPlanVersion = null;
                model.CreditCaseAmortPlanInstallments.Clear();
                return Task.FromResult(model);
            }

            if (!model.IsWorkflowStepEditable.HasValue)
            {
                throw new InvalidOperationException(
                    "IsWorkflowStepEditable must be set before generating the amortization plan.");
            }

            // false (Approved card active) -> Approved* terms; true (Requested card) -> Requested* terms
            var useApprovedTerms = !model.IsWorkflowStepEditable.Value;
            RegenerateCore(model, generatedBy, useApprovedTerms);
            return Task.FromResult(model);
        }

        private void RegenerateCore(
            CreditCaseLoanViewModel model,
            string? generatedBy,
            bool useApprovedTerms)
        {
            var creditCase = model.CreditCase!;
            var settings = ResolveVersion(model);
            model.CreditCaseAmortPlanVersion = settings;

            var startDate = ReadStartDate(creditCase, useApprovedTerms);
            var maturityDate = ReadMaturityDate(creditCase, useApprovedTerms);
            var principal = ReadPrincipal(creditCase, useApprovedTerms);
            var interestRate = ReadInterestRate(creditCase, useApprovedTerms);
            var effectiveInterestRate = ReadEffectiveInterestRate(creditCase, useApprovedTerms);
            var penaltyInterestRate = ReadPenaltyInterestRate(creditCase, useApprovedTerms);
            var graceMonths = Math.Max(
                0,
                useApprovedTerms
                    ? model.ApprovedGracePeriod ?? model.RequestedGracePeriod
                    : model.RequestedGracePeriod);
            var chargeRate = ReadChargeRate(creditCase, useApprovedTerms);
            var minimumChargeAmount = useApprovedTerms
                ? model.ApprovedDisbursementFeeMinimumAmount ?? model.RequestedDisbursementFeeMinimumAmount
                : model.RequestedDisbursementFeeMinimumAmount;

            if (maturityDate <= startDate)
                throw new InvalidOperationException("Maturity date must be after the start date.");
            if (principal <= 0)
                throw new InvalidOperationException("Credit amount must be greater than zero.");

            var derivedGraceType = graceMonths > 0 ? GraceType.InterestOnly : GraceType.None;
            var derivedGraceUntilDate = graceMonths > 0
                ? startDate.ToDateTime(TimeOnly.MinValue).AddMonths(graceMonths)
                : (DateTime?)null;
            var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
            var maturityDateTime = maturityDate.ToDateTime(TimeOnly.MinValue);
            var scheduleDates = BuildScheduleDates(startDateTime, maturityDateTime, settings);
            var appliedChargeAmount = CalculateAppliedChargeAmount(principal, chargeRate, minimumChargeAmount);
            var fees = BuildDisbursementFees(appliedChargeAmount);

            var engineParameters = BuildEngineParameters(
                settings,
                principal,
                interestRate,
                startDateTime,
                maturityDateTime,
                scheduleDates,
                fees,
                derivedGraceType,
                derivedGraceUntilDate);

            var engineResult = AdvancedAmortizationEngine.Generate(engineParameters);
            if (engineResult.Rows.Count == 0)
                throw new InvalidOperationException("The amortization plan did not generate any installments.");

            var installments = MapInstallments(engineResult);
            var version = MapVersion(
                model,
                settings,
                useApprovedTerms,
                generatedBy,
                startDate,
                maturityDate,
                principal,
                interestRate,
                effectiveInterestRate,
                penaltyInterestRate,
                graceMonths,
                engineResult,
                installments);

            model.CreditCaseAmortPlanVersion = version;
            model.CreditCaseAmortPlanInstallments = installments;
            ApplyDerivedFields(model, version, useApprovedTerms);
        }

        private CreditCaseAmortPlanVersionViewModel ResolveVersion(CreditCaseLoanViewModel model)
            => model.CreditCaseAmortPlanVersion
                ?? throw new InvalidOperationException("Amortization plan settings are required.");

        private bool IsDisbursed(string? creditStatusCode)
            => string.Equals(creditStatusCode?.Trim(), "DISBURSED", StringComparison.OrdinalIgnoreCase);

        private AdvancedLoanParameters BuildEngineParameters(
            CreditCaseAmortPlanVersionViewModel settings,
            decimal principal,
            decimal interestRate,
            DateTime startDate,
            DateTime maturityDate,
            List<DateTime> scheduleDates,
            List<DisbursementFee> fees,
            GraceType derivedGraceType,
            DateTime? derivedGraceUntilDate)
        {
            var dayCount = MapDayCountMethod(settings.DayCountCalculationTypeCode, EnumDayCountCalculationType.ActualActual_365_366);
            var principalMethod = MapPrincipalCalculationMethod(settings.PrincipalCalculationMethodCode, PrincipalCalculationMethod.Annuity);
            var graceType = derivedGraceType == GraceType.None
                ? GraceType.None
                : MapGraceType(settings.GraceTypeCode, derivedGraceType);
            var graceUntil = graceType == GraceType.None ? null : derivedGraceUntilDate;

            return new AdvancedLoanParameters
            {
                Principal = principal,
                BaseAnnualRate = interestRate,
                StartDate = startDate,
                EndDate = maturityDate,
                ScheduleDates = scheduleDates,
                DisbursementFees = fees,
                DayCountMethod = dayCount,
                PrincipalCalcMethod = principalMethod,
                FixedInstallmentAmount = settings.FixedInstallmentAmount,
                FixedPrincipalAmount = settings.FixedPrincipalAmount,
                UseIndexedRate = settings.UseIndexedRate,
                IndexCode = settings.UseIndexedRate ? settings.IndexCode : null,
                IndexRatePercent = settings.UseIndexedRate ? settings.IndexRatePercent ?? 0m : 0m,
                CurrencyDecimals = 2,
                RoundingMode = RoundingMode.AwayFromZero,
                GraceType = graceType,
                GraceUntilDate = graceUntil,
                AccrueInterestDuringFullGrace = graceType == GraceType.FullGrace,
                CapitalizeDuringFullGrace = false,
                BalloonPolicy = BalloonPolicy.None,
                Balloons = new List<BalloonPayment>()
            };
        }

        private List<DateTime> BuildScheduleDates(
            DateTime startDate,
            DateTime maturityDate,
            CreditCaseAmortPlanVersionViewModel settings)
        {
            var frequency = MapFrequencyCode(settings.FrequencyCode, FrequencyCode.Monthly);
            var dueDayPolicy = MapDueDayPolicy(settings.DueDayPolicyCode, DueDayPolicy.EndOfMonth);
            var fixedDayOfMonth = Math.Clamp(settings.FixedDayOfMonth ?? 28, 1, 31);
            var firstDueDate = settings.FirstDueDate?.ToDateTime(TimeOnly.MinValue);

            if (frequency == FrequencyCode.AtMaturity)
                return new List<DateTime> { maturityDate.Date };

            var resolvedFirstDueDate = firstDueDate ?? ResolveFirstInstallmentDate(
                startDate,
                maturityDate,
                null,
                frequency,
                dueDayPolicy,
                fixedDayOfMonth);

            if (TryGetMonthInterval(frequency, out var intervalMonths))
            {
                return BuildMonthBasedSchedule(
                    startDate,
                    maturityDate,
                    resolvedFirstDueDate,
                    intervalMonths,
                    dueDayPolicy,
                    fixedDayOfMonth);
            }

            return BuildMonthBasedSchedule(
                startDate,
                maturityDate,
                resolvedFirstDueDate,
                1,
                dueDayPolicy,
                fixedDayOfMonth);
        }

        private List<DateTime> BuildMonthBasedSchedule(
            DateTime startDate,
            DateTime endDate,
            DateTime firstDueDate,
            int intervalMonths,
            DueDayPolicy dueDayPolicy,
            int fixedDayOfMonth)
        {
            var dates = new List<DateTime>();
            if (firstDueDate > startDate.Date && firstDueDate < endDate.Date)
                dates.Add(firstDueDate.Date);

            var installmentOffset = 1;
            while (true)
            {
                var due = firstDueDate.Date.AddMonths(installmentOffset * intervalMonths);
                if (due >= endDate.Date)
                    break;

                if (due > firstDueDate.Date)
                    dates.Add(due);

                installmentOffset++;
            }

            AppendMaturityDate(dates, endDate.Date);
            return dates;
        }

        private DateTime ResolveFirstInstallmentDate(
            DateTime startDate,
            DateTime endDate,
            DateTime? nextInstallmentDate,
            FrequencyCode frequencyCode,
            DueDayPolicy dueDayPolicy,
            int fixedDayOfMonth)
        {
            if (nextInstallmentDate.HasValue && nextInstallmentDate.Value.Date > startDate.Date)
                return nextInstallmentDate.Value.Date > endDate.Date ? endDate.Date : nextInstallmentDate.Value.Date;

            if (TryGetMonthInterval(frequencyCode, out var intervalMonths))
            {
                var due = ApplyDueDayPolicy(startDate.Date.AddMonths(intervalMonths), dueDayPolicy, fixedDayOfMonth);
                return due >= endDate.Date ? endDate.Date : due;
            }

            return endDate.Date;
        }

        private void AppendMaturityDate(List<DateTime> dates, DateTime maturityDate)
        {
            if (dates.Count == 0 || dates[^1].Date != maturityDate.Date)
                dates.Add(maturityDate.Date);
        }

        private DateTime ApplyDueDayPolicy(DateTime anchor, DueDayPolicy policy, int fixedDayOfMonth)
            => policy == DueDayPolicy.EndOfMonth
                ? new DateTime(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month))
                : new DateTime(
                    anchor.Year,
                    anchor.Month,
                    Math.Clamp(fixedDayOfMonth, 1, DateTime.DaysInMonth(anchor.Year, anchor.Month)));

        private bool TryGetMonthInterval(FrequencyCode frequencyCode, out int intervalMonths)
        {
            intervalMonths = frequencyCode switch
            {
                FrequencyCode.Monthly => 1,
                FrequencyCode.Every2Months => 2,
                FrequencyCode.Quarterly => 3,
                FrequencyCode.Every4Months => 4,
                FrequencyCode.SemiAnnually => 6,
                FrequencyCode.Annually => 12,
                _ => 0
            };

            return intervalMonths > 0;
        }

        private GraceType MapGraceType(string? value, GraceType fallback)
            => Enum.TryParse<GraceType>(value, true, out var parsed) ? parsed : fallback;

        private FrequencyCode MapFrequencyCode(string? value, FrequencyCode fallback)
            => Enum.TryParse<FrequencyCode>(value, true, out var parsed) ? parsed : fallback;

        private DueDayPolicy MapDueDayPolicy(string? value, DueDayPolicy fallback)
            => Enum.TryParse<DueDayPolicy>(value, true, out var parsed) ? parsed : fallback;

        private EnumDayCountCalculationType MapDayCountMethod(string? value, EnumDayCountCalculationType fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return Enum.TryParse<EnumDayCountCalculationType>(value, true, out var parsed) ? parsed : fallback;
        }

        private PrincipalCalculationMethod MapPrincipalCalculationMethod(string? value, PrincipalCalculationMethod fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Trim().ToUpperInvariant() switch
            {
                "ANNUITY" => PrincipalCalculationMethod.Annuity,
                "EQUALPRINCIPAL" or "FIXEDPRINCIPAL" => PrincipalCalculationMethod.FixedPrincipal,
                "BULLET" => PrincipalCalculationMethod.Bullet,
                "FIXEDINSTALLMENT" => PrincipalCalculationMethod.FixedInstallment,
                _ => Enum.TryParse<PrincipalCalculationMethod>(value, true, out var parsed) ? parsed : fallback
            };
        }

        private DateOnly ReadStartDate(CreditCaseViewModel creditCase, bool useApprovedTerms)
        {
            if (useApprovedTerms)
            {
                if (!creditCase.StartDateApproved.HasValue)
                    throw new InvalidOperationException("Approved start date is required.");
                return creditCase.StartDateApproved.Value;
            }

            return creditCase.StartDateRequested;
        }

        private DateOnly ReadMaturityDate(CreditCaseViewModel creditCase, bool useApprovedTerms)
        {
            if (useApprovedTerms)
            {
                if (!creditCase.MaturityDateApproved.HasValue)
                    throw new InvalidOperationException("Approved maturity date is required.");
                return creditCase.MaturityDateApproved.Value;
            }

            return creditCase.MaturityDateRequested;
        }

        private decimal ReadPrincipal(CreditCaseViewModel creditCase, bool useApprovedTerms)
        {
            if (useApprovedTerms)
            {
                if (!creditCase.CreditAmountApproved.HasValue || creditCase.CreditAmountApproved.Value <= 0)
                    throw new InvalidOperationException("Approved credit amount is required.");
                return creditCase.CreditAmountApproved.Value;
            }

            return creditCase.CreditAmountRequested;
        }

        private decimal ReadInterestRate(CreditCaseViewModel creditCase, bool useApprovedTerms)
        {
            if (useApprovedTerms)
            {
                if (!creditCase.InterestRateApproved.HasValue)
                    throw new InvalidOperationException("Approved interest rate is required.");
                return creditCase.InterestRateApproved.Value;
            }

            return creditCase.InterestRateRequested;
        }

        private decimal ReadEffectiveInterestRate(CreditCaseViewModel creditCase, bool useApprovedTerms)
            => useApprovedTerms
                ? creditCase.EffectiveInterestRateApproved ?? creditCase.EffectiveInterestRateRequested
                : creditCase.EffectiveInterestRateRequested;

        private decimal ReadPenaltyInterestRate(CreditCaseViewModel creditCase, bool useApprovedTerms)
            => useApprovedTerms
                ? creditCase.PenatlyInterestApproved ?? creditCase.PenatlyInterestRequested
                : creditCase.PenatlyInterestRequested;

        private decimal ReadChargeRate(CreditCaseViewModel creditCase, bool useApprovedTerms)
            => useApprovedTerms
                ? creditCase.PricingChargeRateApproved ?? creditCase.PricingChargeRateRequested
                : creditCase.PricingChargeRateRequested;

        private decimal CalculateAppliedChargeAmount(decimal principal, decimal chargeRate, decimal minimumChargeAmount)
        {
            if (chargeRate == 0m || principal <= 0m)
                return 0m;

            var percentageChargeAmount = Math.Round(
                principal * chargeRate / 100m,
                2,
                MidpointRounding.AwayFromZero);

            return Math.Max(percentageChargeAmount, minimumChargeAmount);
        }

        private List<DisbursementFee> BuildDisbursementFees(decimal appliedChargeAmount)
        {
            if (appliedChargeAmount <= 0m)
                return new List<DisbursementFee>();

            return new List<DisbursementFee>
            {
                new()
                {
                    Name = "Disbursement Fee",
                    FeeAmount = appliedChargeAmount,
                    FeePercent = 0m
                }
            };
        }

        private List<CreditCaseAmortPlanInstallmentViewModel> MapInstallments(AdvancedAmortizationResult result)
            => result.Rows.Select(row =>
                new CreditCaseAmortPlanInstallmentViewModel
                {
                    InstallmentNumber = row.Index,
                    ScheduledFromDate = DateOnly.FromDateTime(row.FromDate),
                    ScheduledToDate = DateOnly.FromDateTime(row.ToDate),
                    ScheduledDueDate = DateOnly.FromDateTime(row.ToDate),
                    IntervalDays = row.AccruedDays,
                    ScheduledPrincipal = row.Principal,
                    ScheduledBaseInterest = row.Interest,
                    ScheduledOtherInterest = 0m,
                    ScheduledInterest = row.Interest,
                    ScheduledFee = 0m,
                    ScheduledInstallmentAmount = row.Total,
                    AppliedAnnualRate = row.TotalRate,
                    PaidPrincipal = 0m,
                    PaidInterest = 0m,
                    PaidFee = 0m,
                    RemainingPrincipal = row.Principal,
                    RemainingInterest = row.Interest,
                    RemainingFee = 0m,
                    LastUpdatedAt = DateTime.UtcNow,
                    IsRecheduled = false,
                    ResheduledDescription = string.Empty,
                    IsPaid = false,
                    IsActive = true,
                    OpeningPrincipalBalance = row.BalanceBefore,
                    ClosingPrincipalBalance = row.BalanceAfter,
                    OpeningInterestBalance = 0m,
                    ClosingInterestBalance = 0m,
                    InstallmentStatusCode = "SCHEDULED",
                    DaysPastDue = 0,
                    ScheduledRateAdjustmentInterest = 0m,
                    IsRateResetApplied = false,
                    InterestCalculationNotes = row.Notes
                }).ToList();

        private CreditCaseAmortPlanVersionViewModel MapVersion(
            CreditCaseLoanViewModel model,
            CreditCaseAmortPlanVersionViewModel settings,
            bool useApprovedTerms,
            string? generatedBy,
            DateOnly startDate,
            DateOnly maturityDate,
            decimal principal,
            decimal interestRate,
            decimal effectiveInterestRate,
            decimal penaltyInterestRate,
            int graceMonths,
            AdvancedAmortizationResult engineResult,
            List<CreditCaseAmortPlanInstallmentViewModel> installments)
        {
            var calculatedChargeRate = principal > 0m
                ? Math.Round((engineResult.TotalDisbursementFees / principal) * 100m, 4, MidpointRounding.AwayFromZero)
                : 0m;
            var resolvedEffectiveRate = engineResult.EffectiveAnnualRate.HasValue
                ? engineResult.EffectiveAnnualRate.Value * 100m
                : effectiveInterestRate;
            var graceType = graceMonths <= 0
                ? GraceType.None
                : MapGraceType(
                    settings.GraceTypeCode,
                    graceMonths > 0 ? GraceType.InterestOnly : GraceType.None);
            var graceUntilDate = graceType == GraceType.None
                ? (DateTime?)null
                : startDate.ToDateTime(TimeOnly.MinValue).AddMonths(graceMonths);

            return new CreditCaseAmortPlanVersionViewModel
            {
                CreditCaseId = model.CreditCaseId > 0 ? model.CreditCaseId : settings.CreditCaseId,
                EffectiveFromDate = startDate,
                EffectiveToDate = maturityDate,
                MaturityDate = maturityDate,
                FirstDueDate = DateOnly.FromDateTime(engineResult.Rows[0].ToDate),
                IsCurrent = true,
                IsLocked = useApprovedTerms,
                IsSuperseded = false,
                AmortizationTemplateCode = string.IsNullOrWhiteSpace(settings.AmortizationTemplateCode)
                    ? null
                    : settings.AmortizationTemplateCode,
                PrincipalAmount = principal,
                BaseInterestRate = interestRate,
                EffectiveInterestRate = resolvedEffectiveRate,
                PenaltyInterestRate = penaltyInterestRate,
                UseIndexedRate = settings.UseIndexedRate,
                IndexCode = settings.UseIndexedRate ? settings.IndexCode : null,
                IndexRatePercent = settings.UseIndexedRate ? settings.IndexRatePercent : null,
                PrincipalCalculationMethodCode = settings.PrincipalCalculationMethodCode
                    ?? PrincipalCalculationMethod.Annuity.ToString(),
                FixedInstallmentAmount = settings.FixedInstallmentAmount,
                FixedPrincipalAmount = settings.FixedPrincipalAmount,
                FrequencyCode = settings.FrequencyCode ?? FrequencyCode.Monthly.ToString(),
                DueDayPolicyCode = settings.DueDayPolicyCode ?? DueDayPolicy.FixedDayOfMonth.ToString(),
                FixedDayOfMonth = settings.FixedDayOfMonth,
                DayCountCalculationTypeCode = settings.DayCountCalculationTypeCode
                    ?? EnumDayCountCalculationType.ActualActual_365_366.ToString(),
                GraceTypeCode = graceType.ToString(),
                GraceUntilDate = graceUntilDate.HasValue ? DateOnly.FromDateTime(graceUntilDate.Value) : null,
                AccrueInterestDuringFullGrace = graceType == GraceType.FullGrace,
                CapitalizeDuringFullGrace = false,
                AddFinalBalloonAll = false,
                TotalDisbursementFeePercent = calculatedChargeRate,
                TotalDisbursementFeeAmount = engineResult.TotalDisbursementFees,
                NetDisbursedAmount = engineResult.NetDisbursedAmount,
                TotalScheduledPrincipal = engineResult.TotalPrincipal,
                TotalScheduledInterest = engineResult.TotalInterest,
                TotalScheduledFee = 0m,
                TotalScheduledAmount = engineResult.TotalPayment,
                InstallmentCount = installments.Count,
                EffectiveAnnualRate = resolvedEffectiveRate,
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = generatedBy,
                Notes = useApprovedTerms
                    ? "Regenerated automatically while updating the loan application."
                    : "Generated automatically while adding/updating the loan application.",
                IsActive = true,
                RequestedGracePeriodMonths = graceMonths,
                CreditCaseAmortPlanInstallments = installments
            };
        }

        private void ApplyDerivedFields(
            CreditCaseLoanViewModel model,
            CreditCaseAmortPlanVersionViewModel version,
            bool useApprovedTerms)
        {
            var creditCase = model.CreditCase!;
            var feeAmount = version.TotalDisbursementFeeAmount ?? 0m;
            var feeRate = version.TotalDisbursementFeePercent ?? 0m;
            var maturityDate = version.MaturityDate ?? version.EffectiveToDate ?? version.EffectiveFromDate;
            var effectiveRate = version.EffectiveAnnualRate
                ?? version.EffectiveInterestRate
                ?? version.BaseInterestRate;

            model.LoanTotalInterestAmount = version.TotalScheduledInterest ?? 0m;
            model.LoanDisbursementFeeAmount = feeAmount;
            model.LoanNetDisbursementAmount = version.NetDisbursedAmount;

            creditCase.TotalAdditionalChargeAmount = feeAmount;
            creditCase.TotalAdditionalChargeAmountForEIR = feeAmount;

            if (useApprovedTerms)
            {
                creditCase.StartDateApproved = version.EffectiveFromDate;
                creditCase.MaturityDateApproved = maturityDate;
                creditCase.CreditAmountApproved = version.PrincipalAmount;
                creditCase.InterestRateApproved = version.BaseInterestRate;
                creditCase.PricingChargeRateApproved = feeRate;
                creditCase.PricingChargeAmountApproved = feeAmount;
                creditCase.EffectiveInterestRateApproved = effectiveRate;
                creditCase.PenatlyInterestApproved = version.PenaltyInterestRate ?? creditCase.PenatlyInterestApproved ?? 0m;
                model.ApprovedGracePeriod = version.RequestedGracePeriodMonths ?? model.ApprovedGracePeriod ?? model.RequestedGracePeriod;
            }
            else
            {
                creditCase.StartDateRequested = version.EffectiveFromDate;
                creditCase.MaturityDateRequested = maturityDate;
                creditCase.CreditAmountRequested = version.PrincipalAmount;
                creditCase.InterestRateRequested = version.BaseInterestRate;
                creditCase.PricingChargeRateRequested = feeRate;
                creditCase.PricingChargeAmountRequested = feeAmount;
                creditCase.EffectiveInterestRateRequested = effectiveRate;
                creditCase.PenatlyInterestRequested = version.PenaltyInterestRate ?? creditCase.PenatlyInterestRequested;
                model.RequestedGracePeriod = version.RequestedGracePeriodMonths ?? model.RequestedGracePeriod;
            }

            foreach (var installment in version.CreditCaseAmortPlanInstallments)
                installment.CreditCaseId = model.CreditCaseId;

            model.CreditCaseAmortPlanInstallments = version.CreditCaseAmortPlanInstallments.ToList();
        }
    }
}
