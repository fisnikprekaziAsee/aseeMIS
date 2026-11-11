using dsBankParty.Core.Services.Interfaces;
using dsBankParty.Core.ViewModels;
using dsMIS.Core.DTOs;
using dsMIS.Core.Enums;
using dsCredit.Core.Repositories.Interfaces;
using dsCredit.Core.Services.Interfaces;
using dsCredit.Core.ViewModels;
using dsCreditDef.Core.Services.Interfaces;
using dsCreditDef.Core.ViewModels;
using dsCreditProduct.Core.Services.Interfaces;
using dsCreditProduct.Core.ViewModels;
using dsLoanEngine.Core.Engine;
using dsLoanEngine.Core.Enums;
using dsLoanEngine.Core.Models;
using aseeMIS.Areas.Credit.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace aseeMIS.Areas.Credit.Controllers
{
    /// <summary>
    /// Amortization plan generator within the Credit area.
    /// Supports both the existing full-page amortization flow and the Loan360 amortization tab flow.
    /// </summary>
    [Area("Credit")]
    public class AmortPlanController(
        IBankPartyService<BankPartyViewModel> bankParties,
        ICreditDefAmortizationTemplateService templates,
        ICreditProductService products,
        ICreditCaseService creditCases,
        ICreditCaseRepository creditCaseRepository,
        ICreditCaseLoanService creditCaseLoans,
        ICreditCaseLoanRepository creditCaseLoanRepository) : Controller
    {
        // ─────────────────────────────────────────────────────────────────────
        // GET: Index
        // ─────────────────────────────────────────────────────────────────────

        /*
        // Legacy full-page amortization screen. Loan360 uses LoadLoanTab/GenerateLoanTab/SaveLoanTab.
        [HttpGet]
        public async Task<IActionResult> Index(...) { ... }
        */

        [HttpGet]
        public async Task<IActionResult> GetTemplateDefaults(
            string? templateCode,
            DateTime? startDate = null,
            int? termMonths = null,
            decimal? baseAnnualRate = null,
            decimal? indexRatePercent = null,
            string? indexCode = null)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
                return Json(new { success = true, defaults = (object?)null });

            var templateVm = await GetTemplateByCodeAsync(templateCode);
            if (templateVm == null)
                return NotFound(new { success = false, message = $"Template '{templateCode}' was not found." });

            var vm = new CreditCaseAmortPlanInputViewModel
            {
                SelectedTemplateCode = templateVm.TemplateCode,
                StartDate = startDate?.Date ?? DateTime.Today,
                TermMonths = Math.Max(1, termMonths ?? 1),
                NextInstallmentDate = null,
                BaseAnnualRate = baseAnnualRate ?? 0m,
                IndexRatePercent = indexRatePercent ?? 0m,
                IndexCode = string.IsNullOrWhiteSpace(indexCode) ? "EURIBOR_3M" : indexCode,
                CurrencyDecimals = 2,
                RoundingMode = RoundingMode.AwayFromZero
            };

            ApplyTemplateTo(templateVm, vm);
            NormalizeDerivedFields(vm);

            return Json(new
            {
                success = true,
                defaults = new
                {
                    templateCode = vm.SelectedTemplateCode,
                    templateName = vm.TemplateName,
                    templateDescription = vm.TemplateDescription,
                    templateAmortizationMethodCode = vm.TemplateAmortizationMethodCode,
                    templatePaymentMethodCode = vm.TemplatePaymentMethodCode,
                    interestPostingFrequencyCode = vm.InterestPostingFrequencyCode,
                    allowSeasonal = vm.AllowSeasonal,
                    installmentMonthsMask = vm.InstallmentMonthsMask,
                    graceMonthsCount = vm.GraceMonthsCount,
                    graceMonthsMaskPrincipal = vm.GraceMonthsMaskPrincipal,
                    graceMonthsMaskInterest = vm.GraceMonthsMaskInterest,
                    rateTypeCode = vm.RateTypeCode,
                    rateResetFrequency = vm.RateResetFrequency,
                    rateMarginBps = vm.RateMarginBps,
                    lastInstallmentAdjustCode = vm.LastInstallmentAdjustCode,
                    frequencyCode = (int)vm.FrequencyCode,
                    dueDayPolicy = (int)vm.DueDayPolicy,
                    fixedDayOfMonth = vm.FixedDayOfMonth,
                    nextInstallmentDate = vm.NextInstallmentDate?.ToString("yyyy-MM-dd"),
                    graceType = (int)vm.GraceType,
                    graceUntilDate = vm.GraceUntilDate?.ToString("yyyy-MM-dd"),
                    accrueInterestDuringFullGrace = vm.AccrueInterestDuringFullGrace,
                    capitalizeDuringFullGrace = vm.CapitalizeDuringFullGrace,
                    addFinalBalloonAll = vm.AddFinalBalloonAll,
                    useIndexedRate = vm.UseIndexedRate,
                    indexCode = vm.IndexCode,
                    baseAnnualRate = vm.BaseAnnualRate,
                    dayCountMethod = (int)vm.DayCountMethod,
                    principalCalcMethod = (int)vm.PrincipalCalcMethod,
                    currencyDecimals = vm.CurrencyDecimals,
                    roundingMode = (int)vm.RoundingMode
                }
            });
        }

        /*
        // Legacy full-page lookup endpoints. The Hub already carries bank party/case context.
        [HttpGet]
        public async Task<IActionResult> GetCreditCasesByBankParty(int bankPartyId) { ... }

        [HttpGet]
        public async Task<IActionResult> GetCreditCaseDefaults(long creditCaseId) { ... }
        */

        // ─────────────────────────────────────────────────────────────────────
        // POST: Index  (generate preview)
        // ─────────────────────────────────────────────────────────────────────

        // Loan360 amortization tab endpoints.
        [HttpGet]
        public async Task<IActionResult> LoadLoanTab(
            int? bankPartyId = null,
            long? creditCaseId = null,
            string? productCode = null,
            string? creditCaseNumber = null)
        {
            var vm = await BuildLoanTabViewModelAsync(bankPartyId, creditCaseId, productCode, creditCaseNumber);
            return PartialView("~/Areas/Credit/Views/Loan/_CreditCaseLoanAmortPlan.cshtml", vm);
        }

        [HttpGet]
        public IActionResult LoadCreditCaseAmortPlanGrid(long creditCaseId)
        {
            if (creditCaseId <= 0)
                return Content("Credit case is required to load amortization plan lines.");

            return ViewComponent(
                "GridDetailWidget",
                new aseeMIS.Areas.Common.DTOs.GridRequest
                {
                    FormId = 332,
                    ForeignAttributeName = "CreditCaseId",
                    TargetId = creditCaseId.ToString(),
                    ContextFormType = EnumContextFormType.CHILD,
                    GridType = "CRUD",
                    RemoteMode = false,
                    GridName = $"GRID_CreditGL_332_{creditCaseId}"
                });
        }

        [HttpGet]
        public async Task<IActionResult> LoadCreditCaseAmortPlanPreview(long creditCaseId)
        {
            if (creditCaseId <= 0)
                return Content("Credit case is required to load amortization plan.");

            var loan = await creditCaseLoanRepository.SelectByIdAsync(creditCaseId, HttpContext.RequestAborted);
            if (loan?.CreditCase == null)
                return Content("Loan application was not found.");

            var creditCase = loan.CreditCase;
            var version = loan.CreditCaseAmortPlanVersion;
            var installments = loan.CreditCaseAmortPlanInstallments ?? new List<CreditCaseAmortPlanInstallmentViewModel>();

            var model = new CreditCaseAmortPlanPreviewViewModel
            {
                CreditCaseId = creditCase.CreditCaseId,
                CaseNumber = creditCase.CaseNumber,
                ProductCode = creditCase.ProductCode,
                ProductName = creditCase.ProductName,
                CurrencyCode = creditCase.CurrencyCode,
                CreditStatusCode = creditCase.CreditStatusCode,
                StageCode = creditCase.StageCode,
                ApprovedEir = creditCase.EffectiveInterestRateApproved,
                Version = version,
                Installments = installments
            };

            return PartialView("~/Areas/Credit/Views/Loan/_CreditCaseAmortPlanPreview.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateLoanTab(CreditLoanAmortPlanTabViewModel vm)
        {
            await PrepareLoanTabViewModelAsync(vm);
            NormalizeLoanTabDerivedFields(vm);

            if (!ValidateLoanTabInput(vm))
                return PartialView("~/Areas/Credit/Views/Loan/_CreditCaseLoanAmortPlan.cshtml", vm);

            try
            {
                var inputVm = MapToInputViewModel(vm);
                GenerateSchedule(inputVm);

                vm = MapToLoanTabViewModel(inputVm);
                await PrepareLoanTabViewModelAsync(vm);
            }
            catch (Exception ex)
            {
                vm.ErrorMessage = $"Error generating schedule: {ex.Message}";
            }

            return PartialView("~/Areas/Credit/Views/Loan/_CreditCaseLoanAmortPlan.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveLoanTab(CreditLoanAmortPlanTabViewModel vm)
        {
            await PrepareLoanTabViewModelAsync(vm);
            NormalizeLoanTabDerivedFields(vm);

            if (!vm.CreditCaseId.HasValue || vm.CreditCaseId.Value <= 0)
                ModelState.AddModelError(nameof(vm.CreditCaseId), "Save the loan application first to persist the amortization plan.");

            if (!ValidateLoanTabInput(vm))
                return PartialView("~/Areas/Credit/Views/Loan/_CreditCaseLoanAmortPlan.cshtml", vm);

            try
            {
                var inputVm = MapToInputViewModel(vm);
                GenerateSchedule(inputVm);
                await SaveGeneratedPlanAsync(inputVm, vm.TermsEditMode);

                vm = MapToLoanTabViewModel(inputVm);
                vm.SuccessMessage = "Amortization plan saved successfully.";
                await PrepareLoanTabViewModelAsync(vm);
            }
            catch (Exception ex)
            {
                vm.ErrorMessage = $"Error saving plan: {ex.Message}";
            }

            return PartialView("~/Areas/Credit/Views/Loan/_CreditCaseLoanAmortPlan.cshtml", vm);
        }

        /*
        // Legacy full-page amortization preview flow.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CreditCaseAmortPlanInputViewModel vm) { ... }
        */

        // ─────────────────────────────────────────────────────────────────────
        // POST: SavePlan  (persist version + installments)
        // ─────────────────────────────────────────────────────────────────────

        /*
        // Legacy full-page save/disbursement flow. Hub workflow will handle status transitions separately.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePlan(CreditCaseAmortPlanInputViewModel vm) { ... }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disburse(CreditCaseAmortPlanInputViewModel vm) { ... }
        */


        private async Task SaveGeneratedPlanAsync(CreditCaseAmortPlanInputViewModel vm, string? termsEditMode = null)
        {
            if (vm.Result == null || vm.Result.Rows.Count == 0)
                throw new InvalidOperationException("No schedule rows found to save.");

            long? resolvedCaseId = null;

            // First try SelectedCreditCaseId
            if (vm.SelectedCreditCaseId.HasValue && vm.SelectedCreditCaseId.Value > 0)
            {
                resolvedCaseId = vm.SelectedCreditCaseId.Value;
            }
            // Then fallback to CreditCaseId
            else if (vm.CreditCaseId.HasValue && vm.CreditCaseId.Value > 0)
            {
                resolvedCaseId = vm.CreditCaseId.Value;
            }
            // Then fallback by case number
            else if (!string.IsNullOrWhiteSpace(vm.CreditCaseNumber))
            {
                var allCases = await creditCases.SelectAllAsync();

                var foundCase = allCases.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.CaseNumber) &&
                    string.Equals(x.CaseNumber.Trim(),
                                  vm.CreditCaseNumber.Trim(),
                                  StringComparison.OrdinalIgnoreCase));

                if (foundCase != null)
                    resolvedCaseId = foundCase.CreditCaseId;
            }

            if (!resolvedCaseId.HasValue)
            {
                throw new InvalidOperationException(
                    $"A loan credit case must be selected before saving the plan. " +
                    $"SelectedCreditCaseId={vm.SelectedCreditCaseId}, CreditCaseId={vm.CreditCaseId}, CreditCaseNumber={vm.CreditCaseNumber}");
            }

            var allCreditCases = await creditCases.SelectAllAsync();

            var selectedCreditCase = allCreditCases.FirstOrDefault(x => x.CreditCaseId == resolvedCaseId.Value);

            if (selectedCreditCase == null)
            {
                throw new InvalidOperationException($"Credit case with id {resolvedCaseId.Value} was not found.");
            }

            vm.SelectedCreditCaseId = selectedCreditCase.CreditCaseId;
            vm.CreditCaseId = selectedCreditCase.CreditCaseId;
            vm.CreditCaseNumber = selectedCreditCase.CaseNumber;
            vm.SelectedBankPartyId = selectedCreditCase.BankPartyId;

            var ctx = BuildContextSession();

            var firstDue = vm.Result.Rows.OrderBy(r => r.ToDate).First().ToDate;
            var maturity = vm.Result.Rows.OrderBy(r => r.ToDate).Last().ToDate;
            var installments = BuildInstallmentViewModels(vm, selectedCreditCase.CreditCaseId);
            var effectiveAnnualRatePercent = ConvertRateToPercent(vm.Result.EffectiveAnnualRate);
            var totalDisbursementFeePercent = ResolveDisbursementFeePercent(vm.Principal, vm.TotalDisbursementFeePercent);
            var loanApplication = await LoadLoanApplicationAsync(selectedCreditCase.CreditCaseId);
            var resolvedTermsEditMode = !string.IsNullOrWhiteSpace(termsEditMode)
                ? termsEditMode
                : ResolveAmortTermsEditMode(selectedCreditCase.CreditStatusCode);

            if (loanApplication?.CreditCase != null)
                ApplyLoanTermsToInputViewModel(vm, loanApplication, resolvedTermsEditMode);

            var useApprovedTerms = string.Equals(resolvedTermsEditMode, "approved", StringComparison.OrdinalIgnoreCase);
            var penaltyInterestRate = useApprovedTerms
                ? selectedCreditCase.PenatlyInterestApproved ?? selectedCreditCase.PenatlyInterestRequested
                : selectedCreditCase.PenatlyInterestRequested;

            var loan = await creditCaseLoanRepository.SelectByIdAsync(selectedCreditCase.CreditCaseId)
                ?? throw new InvalidOperationException($"Loan credit case {selectedCreditCase.CreditCaseId} was not found.");

            loan.IsWorkflowStepEditable = !useApprovedTerms;
            loan.CreditCase ??= selectedCreditCase;
            loan.CreditCaseAmortPlanVersion = new CreditCaseAmortPlanVersionViewModel
            {
                CreditCaseId = selectedCreditCase.CreditCaseId,
                EffectiveFromDate = DateOnly.FromDateTime(vm.StartDate.Date),
                EffectiveToDate = DateOnly.FromDateTime(vm.EndDate.Date),
                IsCurrent = true,
                IsLocked = useApprovedTerms,
                IsSuperseded = false,
                AmortizationTemplateCode = string.IsNullOrWhiteSpace(vm.SelectedTemplateCode) ? null : vm.SelectedTemplateCode,
                PrincipalAmount = vm.Principal,
                BaseInterestRate = vm.BaseAnnualRate,
                EffectiveInterestRate = effectiveAnnualRatePercent,
                PenaltyInterestRate = penaltyInterestRate,
                UseIndexedRate = vm.UseIndexedRate,
                IndexCode = vm.UseIndexedRate ? vm.IndexCode : null,
                IndexRatePercent = vm.UseIndexedRate ? vm.IndexRatePercent : null,
                PrincipalCalculationMethodCode = vm.PrincipalCalcMethod.ToString(),
                FixedInstallmentAmount = vm.FixedInstallmentAmount,
                FixedPrincipalAmount = vm.FixedPrincipalAmount,
                FrequencyCode = vm.FrequencyCode.ToString(),
                DueDayPolicyCode = vm.DueDayPolicy.ToString(),
                FixedDayOfMonth = vm.FixedDayOfMonth,
                DayCountCalculationTypeCode = vm.DayCountMethod.ToString(),
                GraceTypeCode = vm.GraceType.ToString(),
                GraceUntilDate = vm.GraceUntilDate.HasValue ? DateOnly.FromDateTime(vm.GraceUntilDate.Value.Date) : null,
                AccrueInterestDuringFullGrace = vm.AccrueInterestDuringFullGrace,
                CapitalizeDuringFullGrace = vm.CapitalizeDuringFullGrace,
                AddFinalBalloonAll = vm.AddFinalBalloonAll,
                TotalDisbursementFeePercent = totalDisbursementFeePercent,
                TotalDisbursementFeeAmount = vm.Result.TotalDisbursementFees,
                NetDisbursedAmount = vm.Result.NetDisbursedAmount,
                TotalScheduledPrincipal = vm.Result.TotalPrincipal,
                TotalScheduledInterest = vm.Result.TotalInterest,
                TotalScheduledFee = 0m,
                TotalScheduledAmount = vm.Result.TotalPayment,
                InstallmentCount = vm.Result.Rows.Count,
                FirstDueDate = DateOnly.FromDateTime(firstDue.Date),
                MaturityDate = DateOnly.FromDateTime(maturity.Date),
                EffectiveAnnualRate = effectiveAnnualRatePercent,
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = ctx.Username,
                RequestedGracePeriodMonths = vm.GraceMonthsCount,
                Notes = $"Generated via AmortPlan tab for case {selectedCreditCase.CaseNumber} ({resolvedTermsEditMode} terms).",
                IsActive = true,
                IsApproved = null,
                CreditCaseAmortPlanInstallments = installments
            };
            loan.CreditCaseAmortPlanInstallments = installments;

            await creditCaseLoans.UpdateAsync(loan, ctx);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private: save generated schedule to database
        // ─────────────────────────────────────────────────────────────────────

        //private async Task SaveGeneratedPlanAsync(CreditCaseAmortPlanInputViewModel vm)
        //{
        //    if (vm.Result == null || vm.Result.Rows.Count == 0)
        //        throw new InvalidOperationException("No schedule rows found to save.");

        //    var loanApplication = await ResolveSelectedCreditCaseAsync(vm);
        //    if (loanApplication?.CreditCase == null)
        //        throw new InvalidOperationException("A loan credit case must be selected before saving the plan.");

        //    var creditCase = loanApplication.CreditCase;

        //    vm.SelectedBankPartyId = creditCase.BankPartyId;
        //    vm.SelectedCreditCaseId = creditCase.CreditCaseId;
        //    vm.CreditCaseId = creditCase.CreditCaseId;
        //    vm.CreditCaseNumber = creditCase.CaseNumber;

        //    var ctx = BuildContextSession();

        //    var firstDue = vm.Result.Rows.OrderBy(r => r.ToDate).First().ToDate;
        //    var maturity = vm.Result.Rows.OrderBy(r => r.ToDate).Last().ToDate;
        //    var installments = BuildInstallmentViewModels(vm, creditCase.CreditCaseId);

        //    var version = new CreditCaseAmortPlanVersionViewModel
        //    {
        //        CreditCaseId                = creditCase.CreditCaseId,
        //        EffectiveFromDate           = DateOnly.FromDateTime(vm.StartDate.Date),
        //        EffectiveToDate             = DateOnly.FromDateTime(vm.EndDate.Date),
        //        IsCurrent                   = true,
        //        IsLocked                    = false,
        //        IsSuperseded                = false,
        //        AmortizationTemplateCode    = string.IsNullOrWhiteSpace(vm.SelectedTemplateCode) ? null : vm.SelectedTemplateCode,
        //        PrincipalAmount             = vm.Principal,
        //        BaseInterestRate            = vm.BaseAnnualRate,
        //        EffectiveInterestRate       = vm.Result.EffectiveAnnualRate,
        //        PenaltyInterestRate         = null,
        //        UseIndexedRate              = vm.UseIndexedRate,
        //        IndexCode                   = vm.UseIndexedRate ? vm.IndexCode : null,
        //        IndexRatePercent            = vm.UseIndexedRate ? vm.IndexRatePercent : null,
        //        PrincipalCalculationMethodCode = vm.PrincipalCalcMethod.ToString(),
        //        FixedInstallmentAmount      = vm.FixedInstallmentAmount,
        //        FixedPrincipalAmount        = vm.FixedPrincipalAmount,
        //        FrequencyCode               = vm.FrequencyCode.ToString(),
        //        DueDayPolicyCode            = vm.DueDayPolicy.ToString(),
        //        FixedDayOfMonth             = vm.FixedDayOfMonth,
        //        DayCountCalculationTypeCode = vm.DayCountMethod.ToString(),
        //        GraceTypeCode               = vm.GraceType.ToString(),
        //        GraceUntilDate              = vm.GraceUntilDate.HasValue ? DateOnly.FromDateTime(vm.GraceUntilDate.Value.Date) : null,
        //        AccrueInterestDuringFullGrace = vm.AccrueInterestDuringFullGrace,
        //        CapitalizeDuringFullGrace   = vm.CapitalizeDuringFullGrace,
        //        AddFinalBalloonAll          = vm.AddFinalBalloonAll,
        //        TotalDisbursementFeePercent = vm.TotalDisbursementFeePercent,
        //        TotalDisbursementFeeAmount  = vm.Result.TotalDisbursementFees,
        //        NetDisbursedAmount          = vm.Result.NetDisbursedAmount,
        //        TotalScheduledPrincipal     = vm.Result.TotalPrincipal,
        //        TotalScheduledInterest      = vm.Result.TotalInterest,
        //        TotalScheduledFee           = 0m,
        //        TotalScheduledAmount        = vm.Result.TotalPayment,
        //        InstallmentCount            = vm.Result.Rows.Count,
        //        FirstDueDate                = DateOnly.FromDateTime(firstDue.Date),
        //        MaturityDate                = DateOnly.FromDateTime(maturity.Date),
        //        EffectiveAnnualRate         = vm.Result.EffectiveAnnualRate,
        //        GeneratedBy                 = ctx.Username,
        //        RequestedGracePeriodMonths  = vm.GraceMonthsCount,
        //        Notes                       = $"Generated via CreditCase/AmortPlan for case {creditCase.CaseNumber}",
        //        IsActive                    = true,
        //        IsApproved                  = null,
        //        CreditCaseAmortPlanInstallments = installments
        //    };

        //    await amortPlanVersions.AddAsync(version, ctx);

        //}

        private async Task<CreditLoanAmortPlanTabViewModel> BuildLoanTabViewModelAsync(
            int? bankPartyId,
            long? creditCaseId,
            string? productCode,
            string? creditCaseNumber)
        {
            var vm = new CreditLoanAmortPlanTabViewModel
            {
                BankPartyId = bankPartyId > 0 ? bankPartyId : null,
                CreditCaseId = creditCaseId > 0 ? creditCaseId : null,
                CreditCaseNumber = NullIfWhiteSpace(creditCaseNumber),
                ProductCode = NullIfWhiteSpace(productCode),
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(1),
                NextInstallmentDate = DateTime.Today.AddMonths(1),
                TermMonths = 1,
                Principal = 10000m,
                BaseAnnualRate = 0m,
                IndexCode = "EURIBOR_3M",
                DayCountMethod = EnumDayCountCalculationType.Actual360,
                PrincipalCalcMethod = PrincipalCalculationMethod.Annuity,
                FrequencyCode = FrequencyCode.Monthly,
                DueDayPolicy = DueDayPolicy.EndOfMonth,
                FixedDayOfMonth = 28,
                CurrencyDecimals = 2,
                RoundingMode = RoundingMode.AwayFromZero,
                TotalDisbursementFeePercent = 0m,
                GraceType = GraceType.None
            };

            if (creditCaseId.HasValue && creditCaseId.Value > 0)
            {
                var seedVm = new CreditCaseAmortPlanInputViewModel
                {
                    CreditCaseId = creditCaseId.Value,
                    SelectedBankPartyId = bankPartyId,
                    SelectedCreditCaseId = creditCaseId.Value,
                    SelectedProductCode = NullIfWhiteSpace(productCode),
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddMonths(12),
                    NextInstallmentDate = DateTime.Today.AddMonths(1),
                    TermMonths = 12,
                    Principal = 10000m,
                    BaseAnnualRate = 5.0m,
                    IndexRatePercent = 0m,
                    IndexCode = "EURIBOR_3M",
                    DayCountMethod = EnumDayCountCalculationType.Actual360,
                    PrincipalCalcMethod = PrincipalCalculationMethod.Annuity,
                    FrequencyCode = FrequencyCode.Monthly,
                    DueDayPolicy = DueDayPolicy.EndOfMonth,
                    FixedDayOfMonth = 28,
                    CurrencyDecimals = 2,
                    RoundingMode = RoundingMode.AwayFromZero,
                    TotalDisbursementFeePercent = 0m,
                    DisbursementFees = new List<DisbursementFeeInput>()
                };

                await ResolveSelectedCreditCaseAsync(seedVm);
                var loanApplication = seedVm.SelectedCreditCaseId.HasValue
                    ? await LoadLoanApplicationAsync(seedVm.SelectedCreditCaseId.Value)
                    : null;
                if (loanApplication?.CreditCase != null)
                {
                    ApplyLoanTermsToInputViewModel(
                        seedVm,
                        loanApplication,
                        ResolveAmortTermsEditMode(loanApplication.CreditCase.CreditStatusCode));
                }

                var currentPlanVersion = await LoadCurrentAmortPlanAsync(creditCaseId.Value);
                if (currentPlanVersion != null && !string.IsNullOrWhiteSpace(currentPlanVersion.AmortizationTemplateCode))
                    seedVm.SelectedTemplateCode = currentPlanVersion.AmortizationTemplateCode;

                var productVm = await GetProductByCodeAsync(seedVm.SelectedProductCode);
                if (productVm != null)
                    ApplyProductTo(productVm, seedVm);

                if (!string.IsNullOrWhiteSpace(seedVm.SelectedTemplateCode))
                {
                    var templateVm = await GetTemplateByCodeAsync(seedVm.SelectedTemplateCode);
                    if (templateVm != null)
                        ApplyTemplateTo(templateVm, seedVm);
                }

                NormalizeDerivedFields(seedVm);

                if (currentPlanVersion != null)
                {
                    ApplyExistingPlanTo(seedVm, currentPlanVersion);
                    NormalizeDerivedFields(seedVm);

                    try
                    {
                        GenerateSchedule(seedVm);
                    }
                    catch (Exception ex)
                    {
                        seedVm.ErrorMessage = $"Error regenerating schedule: {ex.Message}";
                    }
                }

                vm = MapToLoanTabViewModel(seedVm);
            }

            await PrepareLoanTabViewModelAsync(vm);
            return vm;
        }

        private async Task PrepareLoanTabViewModelAsync(CreditLoanAmortPlanTabViewModel vm)
        {
            vm.CreditCaseNumber = NullIfWhiteSpace(vm.CreditCaseNumber);
            vm.ProductCode = NullIfWhiteSpace(vm.ProductCode);
            vm.SelectedTemplateCode = NullIfWhiteSpace(vm.SelectedTemplateCode);
            vm.FixedDayOfMonth = Math.Clamp(vm.FixedDayOfMonth, 1, 31);

            if (vm.CreditCaseId.HasValue && vm.CreditCaseId.Value > 0)
            {
                var loanApplication = await LoadLoanApplicationAsync(vm.CreditCaseId.Value);
                if (loanApplication?.CreditCase != null)
                {
                    vm.BankPartyId ??= loanApplication.CreditCase.BankPartyId;
                    vm.CreditCaseNumber ??= loanApplication.CreditCase.CaseNumber;
                    vm.ProductCode ??= loanApplication.CreditCase.ProductCode;
                }
            }

            await PopulateLoanTabTemplateOptionsAsync(vm, vm.SelectedTemplateCode);

            if (!string.IsNullOrWhiteSpace(vm.ProductCode))
            {
                var productVm = await GetProductByCodeAsync(vm.ProductCode);
                if (productVm != null)
                {
                    vm.ProductAmountMin = productVm.AmountMin;
                    vm.ProductAmountMax = productVm.AmountMax;

                    if ((!vm.Principal.HasValue || vm.Principal.Value <= 0) &&
                        productVm.AmountMin.HasValue &&
                        productVm.AmountMin.Value > 0)
                    {
                        vm.Principal = productVm.AmountMin.Value;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(vm.SelectedTemplateCode))
            {
                var templateVm = await GetTemplateByCodeAsync(vm.SelectedTemplateCode);
                if (templateVm != null)
                    ApplyTemplateMetadata(vm, templateVm);
            }

            NormalizeLoanTabDerivedFields(vm);
            await ApplyActiveLoanTermsFromCreditCaseAsync(vm);
        }

        private async Task ApplyActiveLoanTermsFromCreditCaseAsync(CreditLoanAmortPlanTabViewModel vm)
        {
            if (!vm.CreditCaseId.HasValue || vm.CreditCaseId.Value <= 0)
                return;

            var loanApplication = await LoadLoanApplicationAsync(vm.CreditCaseId.Value);
            if (loanApplication?.CreditCase == null)
                return;

            if (string.IsNullOrWhiteSpace(vm.TermsEditMode))
                vm.TermsEditMode = ResolveAmortTermsEditMode(loanApplication.CreditCase.CreditStatusCode);
            else
                vm.TermsEditMode = string.Equals(vm.TermsEditMode, "approved", StringComparison.OrdinalIgnoreCase)
                    ? "approved"
                    : "requested";

            ApplyLoanTermsToTabViewModel(vm, loanApplication, vm.TermsEditMode);
        }

        private static string ResolveAmortTermsEditMode(string? statusCode)
        {
            var approvedStatusCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "IN_REVIEW",
                "APPROVED",
                "READY_FOR_DISBURSEMENT"
            };

            return !string.IsNullOrWhiteSpace(statusCode) && approvedStatusCodes.Contains(statusCode)
                ? "approved"
                : "requested";
        }

        private static void ApplyLoanTermsToTabViewModel(
            CreditLoanAmortPlanTabViewModel vm,
            CreditCaseLoanApplicationViewModel loanApplication,
            string termsEditMode)
        {
            var creditCase = loanApplication.CreditCase;
            var loanDetails = loanApplication.LoanDetails;
            var useApproved = string.Equals(termsEditMode, "approved", StringComparison.OrdinalIgnoreCase);

            vm.TermsEditMode = useApproved ? "approved" : "requested";
            vm.BankPartyId ??= creditCase.BankPartyId;
            vm.CreditCaseNumber ??= creditCase.CaseNumber;
            vm.ProductCode ??= creditCase.ProductCode;

            if (useApproved)
            {
                vm.StartDate = ToDateTime(creditCase.StartDateApproved ?? creditCase.StartDateRequested);
                vm.EndDate = ToDateTime(creditCase.MaturityDateApproved ?? creditCase.MaturityDateRequested);
                vm.Principal = creditCase.CreditAmountApproved ?? creditCase.CreditAmountRequested;
                vm.BaseAnnualRate = creditCase.InterestRateApproved ?? creditCase.InterestRateRequested;
                vm.DisplayEffectiveInterestRate = creditCase.EffectiveInterestRateApproved
                    ?? creditCase.EffectiveInterestRateRequested;
                vm.TotalDisbursementFeePercent = creditCase.PricingChargeRateApproved
                    ?? creditCase.PricingChargeRateRequested;
                vm.DisplayChargeAmount = creditCase.PricingChargeAmountApproved
                    ?? creditCase.PricingChargeAmountRequested;
                vm.DisplayPenaltyInterest = creditCase.PenatlyInterestApproved ?? creditCase.PenatlyInterestRequested;
                vm.GraceMonthsCount = Math.Max(0, loanDetails?.ApprovedGracePeriod ?? loanDetails?.RequestedGracePeriod ?? 0);
            }
            else
            {
                vm.StartDate = ToDateTime(creditCase.StartDateRequested);
                vm.EndDate = ToDateTime(creditCase.MaturityDateRequested);
                vm.Principal = creditCase.CreditAmountRequested;
                vm.BaseAnnualRate = creditCase.InterestRateRequested;
                vm.DisplayEffectiveInterestRate = creditCase.EffectiveInterestRateRequested;
                vm.TotalDisbursementFeePercent = creditCase.PricingChargeRateRequested;
                vm.DisplayChargeAmount = creditCase.PricingChargeAmountRequested;
                vm.DisplayPenaltyInterest = creditCase.PenatlyInterestRequested;
                vm.GraceMonthsCount = Math.Max(0, loanDetails?.RequestedGracePeriod ?? 0);
            }

            if (vm.StartDate.HasValue && vm.EndDate.HasValue)
                vm.TermMonths = CalculateMonthDifference(vm.StartDate.Value, vm.EndDate.Value);
        }

        private static void ApplyLoanTermsToInputViewModel(
            CreditCaseAmortPlanInputViewModel vm,
            CreditCaseLoanApplicationViewModel loanApplication,
            string termsEditMode)
        {
            var creditCase = loanApplication.CreditCase;
            var loanDetails = loanApplication.LoanDetails;
            var useApproved = string.Equals(termsEditMode, "approved", StringComparison.OrdinalIgnoreCase);

            vm.CreditCaseId = creditCase.CreditCaseId;
            vm.SelectedCreditCaseId = creditCase.CreditCaseId;
            vm.SelectedBankPartyId = creditCase.BankPartyId;
            vm.CreditCaseNumber = creditCase.CaseNumber;
            vm.SelectedProductCode = creditCase.ProductCode;

            if (useApproved)
            {
                vm.StartDate = ToDateTime(creditCase.StartDateApproved ?? creditCase.StartDateRequested);
                vm.EndDate = ToDateTime(creditCase.MaturityDateApproved ?? creditCase.MaturityDateRequested);
                vm.Principal = creditCase.CreditAmountApproved ?? creditCase.CreditAmountRequested;
                vm.BaseAnnualRate = creditCase.InterestRateApproved ?? creditCase.InterestRateRequested;
                vm.TotalDisbursementFeePercent = creditCase.PricingChargeRateApproved
                    ?? creditCase.PricingChargeRateRequested;
                vm.GraceMonthsCount = Math.Max(0, loanDetails?.ApprovedGracePeriod ?? loanDetails?.RequestedGracePeriod ?? 0);
            }
            else
            {
                vm.StartDate = ToDateTime(creditCase.StartDateRequested);
                vm.EndDate = ToDateTime(creditCase.MaturityDateRequested);
                vm.Principal = creditCase.CreditAmountRequested;
                vm.BaseAnnualRate = creditCase.InterestRateRequested;
                vm.TotalDisbursementFeePercent = creditCase.PricingChargeRateRequested;
                vm.GraceMonthsCount = Math.Max(0, loanDetails?.RequestedGracePeriod ?? 0);
            }

            vm.TermMonths = CalculateMonthDifference(vm.StartDate, vm.EndDate);
            vm.NextInstallmentDate = null;
        }

        private bool ValidateLoanTabInput(CreditLoanAmortPlanTabViewModel vm)
        {
            if (!vm.StartDate.HasValue)
                ModelState.AddModelError(nameof(vm.StartDate), "Start date is required.");

            if (!vm.TermMonths.HasValue || vm.TermMonths.Value <= 0)
                ModelState.AddModelError(nameof(vm.TermMonths), "Term months must be greater than zero.");

            if (!vm.EndDate.HasValue)
                ModelState.AddModelError(nameof(vm.EndDate), "End date is required.");

            if (vm.StartDate.HasValue && vm.EndDate.HasValue && vm.EndDate.Value.Date < vm.StartDate.Value.Date)
                ModelState.AddModelError(nameof(vm.EndDate), "End date cannot be earlier than start date.");

            if (!vm.Principal.HasValue || vm.Principal.Value <= 0)
                ModelState.AddModelError(nameof(vm.Principal), "Principal amount must be greater than zero.");

            if (vm.ProductAmountMin.HasValue && vm.Principal.HasValue && vm.Principal.Value < vm.ProductAmountMin.Value)
                ModelState.AddModelError(nameof(vm.Principal), $"Amount < min {vm.ProductAmountMin.Value:N2}");

            if (vm.ProductAmountMax.HasValue && vm.Principal.HasValue && vm.Principal.Value > vm.ProductAmountMax.Value)
                ModelState.AddModelError(nameof(vm.Principal), $"Amount > max {vm.ProductAmountMax.Value:N2}");

            if (vm.PrincipalCalcMethod == PrincipalCalculationMethod.FixedInstallment &&
                (!vm.FixedInstallmentAmount.HasValue || vm.FixedInstallmentAmount.Value <= 0))
            {
                ModelState.AddModelError(nameof(vm.FixedInstallmentAmount), "Fixed installment amount is required.");
            }

            if (vm.PrincipalCalcMethod == PrincipalCalculationMethod.FixedPrincipal &&
                (!vm.FixedPrincipalAmount.HasValue || vm.FixedPrincipalAmount.Value <= 0))
            {
                ModelState.AddModelError(nameof(vm.FixedPrincipalAmount), "Fixed principal amount is required.");
            }

            if (!ModelState.IsValid && string.IsNullOrWhiteSpace(vm.ErrorMessage))
                vm.ErrorMessage = "Please correct the amortization plan fields and try again.";

            return ModelState.IsValid;
        }

        private static void NormalizeLoanTabDerivedFields(CreditLoanAmortPlanTabViewModel vm)
        {
            if (vm.StartDate.HasValue && vm.EndDate.HasValue && vm.EndDate.Value.Date >= vm.StartDate.Value.Date)
            {
                vm.TermMonths = CalculateMonthDifference(vm.StartDate.Value, vm.EndDate.Value);
            }
            else if (vm.StartDate.HasValue && vm.TermMonths.HasValue && vm.TermMonths.Value > 0)
            {
                vm.EndDate = vm.StartDate.Value.Date.AddMonths(vm.TermMonths.Value);
            }

            if (vm.StartDate.HasValue && vm.EndDate.HasValue)
            {
                var nextInstallmentDate = vm.NextInstallmentDate;
                var dueDayPolicy = vm.DueDayPolicy;
                var fixedDayOfMonth = vm.FixedDayOfMonth;
                NormalizeScheduleAnchor(
                    vm.StartDate.Value,
                    vm.EndDate.Value,
                    ref nextInstallmentDate,
                    vm.FrequencyCode,
                    ref dueDayPolicy,
                    ref fixedDayOfMonth,
                    vm.InstallmentMonthsMask);
                vm.NextInstallmentDate = nextInstallmentDate;
                vm.DueDayPolicy = dueDayPolicy;
                vm.FixedDayOfMonth = fixedDayOfMonth;
            }

            if (vm.GraceType == GraceType.None)
            {
                vm.GraceUntilDate = null;
                return;
            }

            if (!vm.GraceUntilDate.HasValue &&
                vm.GraceMonthsCount.HasValue &&
                vm.GraceMonthsCount.Value > 0 &&
                vm.StartDate.HasValue)
            {
                vm.GraceUntilDate = ResolveGraceUntilDateFromSchedule(
                    vm.StartDate.Value,
                    vm.EndDate ?? vm.StartDate.Value.AddMonths(Math.Max(vm.TermMonths ?? 1, 1)),
                    vm.NextInstallmentDate,
                    vm.GraceMonthsCount.Value,
                    vm.FrequencyCode,
                    vm.DueDayPolicy,
                    vm.FixedDayOfMonth,
                    vm.InstallmentMonthsMask);
            }
        }

        private static CreditLoanAmortPlanTabViewModel MapToLoanTabViewModel(CreditCaseAmortPlanInputViewModel source)
        {
            return new CreditLoanAmortPlanTabViewModel
            {
                BankPartyId = source.SelectedBankPartyId,
                CreditCaseId = source.SelectedCreditCaseId ?? source.CreditCaseId,
                CreditCaseNumber = source.CreditCaseNumber,
                ProductCode = source.SelectedProductCode,
                SelectedTemplateCode = NullIfWhiteSpace(source.SelectedTemplateCode),
                TemplateName = source.TemplateName,
                TemplateDescription = source.TemplateDescription,
                TemplateAmortizationMethodCode = source.TemplateAmortizationMethodCode,
                TemplatePaymentMethodCode = source.TemplatePaymentMethodCode,
                InterestPostingFrequencyCode = source.InterestPostingFrequencyCode,
                AllowSeasonal = source.AllowSeasonal,
                InstallmentMonthsMask = source.InstallmentMonthsMask,
                GraceMonthsMaskPrincipal = source.GraceMonthsMaskPrincipal,
                GraceMonthsMaskInterest = source.GraceMonthsMaskInterest,
                RateTypeCode = source.RateTypeCode,
                RateResetFrequency = source.RateResetFrequency,
                RateMarginBps = source.RateMarginBps,
                LastInstallmentAdjustCode = source.LastInstallmentAdjustCode,
                Principal = source.Principal,
                BaseAnnualRate = source.BaseAnnualRate,
                TermMonths = source.TermMonths,
                StartDate = source.StartDate,
                EndDate = source.EndDate,
                NextInstallmentDate = source.NextInstallmentDate,
                UseIndexedRate = source.UseIndexedRate,
                IndexCode = source.IndexCode,
                IndexRatePercent = source.IndexRatePercent,
                TotalDisbursementFeePercent = source.TotalDisbursementFeePercent,
                DayCountMethod = source.DayCountMethod,
                PrincipalCalcMethod = source.PrincipalCalcMethod,
                FixedInstallmentAmount = source.FixedInstallmentAmount,
                FixedPrincipalAmount = source.FixedPrincipalAmount,
                CurrencyDecimals = source.CurrencyDecimals,
                RoundingMode = source.RoundingMode,
                FrequencyCode = source.FrequencyCode,
                DueDayPolicy = source.DueDayPolicy,
                FixedDayOfMonth = source.FixedDayOfMonth,
                GraceMonthsCount = source.GraceMonthsCount,
                GraceType = source.GraceType,
                GraceUntilDate = source.GraceUntilDate,
                AccrueInterestDuringFullGrace = source.AccrueInterestDuringFullGrace,
                CapitalizeDuringFullGrace = source.CapitalizeDuringFullGrace,
                AddFinalBalloonAll = source.AddFinalBalloonAll,
                Result = source.Result,
                ErrorMessage = source.ErrorMessage,
                SuccessMessage = source.SuccessMessage,
                ProductAmountMin = source.ProductAmountMin,
                ProductAmountMax = source.ProductAmountMax
            };
        }

        private static CreditCaseAmortPlanInputViewModel MapToInputViewModel(CreditLoanAmortPlanTabViewModel source)
        {
            var resolvedCreditCaseId = source.CreditCaseId.HasValue && source.CreditCaseId.Value > 0
                ? source.CreditCaseId
                : null;

            var startDate = source.StartDate ?? DateTime.Today;
            var termMonths = source.TermMonths.HasValue && source.TermMonths.Value > 0
                ? source.TermMonths.Value
                : CalculateMonthDifference(startDate, source.EndDate ?? startDate.AddMonths(1));

            var endDate = source.EndDate ?? startDate.AddMonths(termMonths);

            return new CreditCaseAmortPlanInputViewModel
            {
                CreditCaseId = resolvedCreditCaseId,
                SelectedBankPartyId = source.BankPartyId,
                SelectedCreditCaseId = resolvedCreditCaseId,
                CreditCaseNumber = source.CreditCaseNumber,
                SelectedProductCode = source.ProductCode,
                SelectedTemplateCode = NullIfWhiteSpace(source.SelectedTemplateCode),
                TemplateName = source.TemplateName,
                TemplateDescription = source.TemplateDescription,
                TemplateAmortizationMethodCode = source.TemplateAmortizationMethodCode,
                TemplatePaymentMethodCode = source.TemplatePaymentMethodCode,
                InterestPostingFrequencyCode = source.InterestPostingFrequencyCode,
                AllowSeasonal = source.AllowSeasonal,
                InstallmentMonthsMask = source.InstallmentMonthsMask,
                GraceMonthsCount = source.GraceMonthsCount,
                GraceMonthsMaskPrincipal = source.GraceMonthsMaskPrincipal,
                GraceMonthsMaskInterest = source.GraceMonthsMaskInterest,
                RateTypeCode = source.RateTypeCode,
                RateResetFrequency = source.RateResetFrequency,
                RateMarginBps = source.RateMarginBps,
                LastInstallmentAdjustCode = source.LastInstallmentAdjustCode,
                Principal = source.Principal ?? 0m,
                BaseAnnualRate = source.BaseAnnualRate ?? 0m,
                TermMonths = termMonths,
                StartDate = startDate,
                EndDate = endDate,
                NextInstallmentDate = source.NextInstallmentDate,
                UseIndexedRate = source.UseIndexedRate,
                IndexCode = string.IsNullOrWhiteSpace(source.IndexCode) ? "EURIBOR_3M" : source.IndexCode,
                IndexRatePercent = source.IndexRatePercent ?? 0m,
                DisbursementFees = new List<DisbursementFeeInput>(),
                TotalDisbursementFeePercent = source.TotalDisbursementFeePercent ?? 0m,
                DayCountMethod = source.DayCountMethod,
                PrincipalCalcMethod = source.PrincipalCalcMethod,
                FixedInstallmentAmount = source.FixedInstallmentAmount,
                FixedPrincipalAmount = source.FixedPrincipalAmount,
                CurrencyDecimals = source.CurrencyDecimals,
                RoundingMode = source.RoundingMode,
                FrequencyCode = source.FrequencyCode,
                DueDayPolicy = source.DueDayPolicy,
                FixedDayOfMonth = source.FixedDayOfMonth,
                GraceType = source.GraceType,
                GraceUntilDate = source.GraceUntilDate,
                AccrueInterestDuringFullGrace = source.AccrueInterestDuringFullGrace,
                CapitalizeDuringFullGrace = source.CapitalizeDuringFullGrace,
                AddFinalBalloonAll = source.AddFinalBalloonAll,
                ProductAmountMin = source.ProductAmountMin,
                ProductAmountMax = source.ProductAmountMax
            };
        }

        private async Task PopulateLoanTabTemplateOptionsAsync(CreditLoanAmortPlanTabViewModel vm, string? preselect = null)
        {
            var list = await templates.SelectAllAsync();
            var opts = list.Where(t => t.IsActive && (t.IsApproved ?? false))
                           .OrderBy(t => t.TemplateName)
                           .Select(t => new KeyValuePair<string, string>(t.TemplateCode, t.TemplateName))
                           .ToList();

            opts.Insert(0, new KeyValuePair<string, string>(string.Empty, "(No Template - Use Custom Parameters)"));

            vm.TemplateOptions = opts;
            vm.SelectedTemplateCode = preselect;
        }

        private static void ApplyTemplateMetadata(
            CreditLoanAmortPlanTabViewModel vm,
            CreditDefAmortizationTemplateViewModel template)
        {
            vm.TemplateName = template.TemplateName;
            vm.TemplateDescription = template.TemplateDescription;
            vm.TemplateAmortizationMethodCode = template.AmortizationMethodCode;
            vm.TemplatePaymentMethodCode = template.PaymentMethodCode;
            vm.InterestPostingFrequencyCode = template.InterestPostingFrequencyselectCode;
            vm.AllowSeasonal = template.AllowSeasonal;
            vm.InstallmentMonthsMask = NormalizeMonthMask(template.InstallmentMonthsMask);
            vm.GraceMonthsMaskPrincipal = NormalizeMonthMask(template.GraceMonthsMaskPrincipal);
            vm.GraceMonthsMaskInterest = NormalizeMonthMask(template.GraceMonthsMaskInterest);
            vm.RateTypeCode = template.RateTypeCode;
            vm.RateResetFrequency = template.RateResetFrequency;
            vm.RateMarginBps = template.RateMarginBps;
            vm.LastInstallmentAdjustCode = template.LastInstallmentAdjustCode;
        }

        private static string? NullIfWhiteSpace(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static List<CreditCaseAmortPlanInstallmentViewModel> BuildInstallmentViewModels(
            CreditCaseAmortPlanInputViewModel vm,
            long creditCaseId)
        {
            if (vm.Result == null)
                return new List<CreditCaseAmortPlanInstallmentViewModel>();

            return vm.Result.Rows.Select(row =>
            {
                decimal scheduledBaseInterest = row.Interest;
                decimal scheduledOtherInterest = 0m;

                if (vm.UseIndexedRate && row.TotalRate > 0)
                {
                    var baseRatio = row.BaseRate / row.TotalRate;
                    scheduledBaseInterest = Math.Round(row.Interest * baseRatio, 2, MidpointRounding.AwayFromZero);
                    scheduledOtherInterest = row.Interest - scheduledBaseInterest;
                }

                return new CreditCaseAmortPlanInstallmentViewModel
                {
                    CreditCaseId = creditCaseId,
                    InstallmentNumber = row.Index,
                    ScheduledFromDate = DateOnly.FromDateTime(row.FromDate.Date),
                    ScheduledToDate = DateOnly.FromDateTime(row.ToDate.Date),
                    ScheduledDueDate = DateOnly.FromDateTime(row.ToDate.Date),
                    IntervalDays = row.AccruedDays,
                    ScheduledPrincipal = row.Principal,
                    ScheduledBaseInterest = scheduledBaseInterest,
                    ScheduledOtherInterest = scheduledOtherInterest,
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
                };
            }).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private: schedule generation
        // ─────────────────────────────────────────────────────────────────────

        private static void GenerateSchedule(CreditCaseAmortPlanInputViewModel vm)
        {
            var scheduleDates = BuildScheduleDates(vm);
            vm.TotalDisbursementFeePercent = ResolveDisbursementFeePercent(vm.Principal, vm.TotalDisbursementFeePercent);

            var fees = new List<DisbursementFee>();
            if (vm.TotalDisbursementFeePercent > 0)
            {
                fees.Add(new DisbursementFee
                {
                    Name       = "Disbursement Fee",
                    FeePercent = vm.TotalDisbursementFeePercent,
                    FeeAmount  = 0m
                });
            }

            foreach (var f in vm.DisbursementFees)
            {
                if (f.FeePercent > 0 || f.FeeAmount > 0)
                    fees.Add(new DisbursementFee { Name = f.Name, FeePercent = f.FeePercent, FeeAmount = f.FeeAmount });
            }

            var parameters = new AdvancedLoanParameters
            {
                Principal              = vm.Principal,
                BaseAnnualRate         = vm.BaseAnnualRate,
                StartDate              = vm.StartDate,
                EndDate                = vm.EndDate,
                ScheduleDates          = scheduleDates,
                UseIndexedRate         = vm.UseIndexedRate,
                IndexCode              = vm.IndexCode,
                IndexRatePercent       = vm.IndexRatePercent,
                DisbursementFees       = fees,
                DayCountMethod         = vm.DayCountMethod,
                PrincipalCalcMethod    = vm.PrincipalCalcMethod,
                FixedInstallmentAmount = vm.FixedInstallmentAmount,
                FixedPrincipalAmount   = vm.FixedPrincipalAmount,
                CurrencyDecimals       = vm.CurrencyDecimals,
                RoundingMode           = vm.RoundingMode,
                GraceType              = vm.GraceType,
                GraceUntilDate         = vm.GraceUntilDate,
                AccrueInterestDuringFullGrace = vm.AccrueInterestDuringFullGrace,
                CapitalizeDuringFullGrace     = vm.CapitalizeDuringFullGrace,
                BalloonPolicy          = vm.AddFinalBalloonAll ? BalloonPolicy.AtMaturityAll : BalloonPolicy.None,
                Balloons               = new List<BalloonPayment>()
            };

            if (vm.AddFinalBalloonAll)
                parameters.Balloons.Add(new BalloonPayment { Date = vm.EndDate, Amount = null });

            vm.Result = AdvancedAmortizationEngine.Generate(parameters);
        }

        private static decimal ResolveDisbursementFeePercent(decimal principal, decimal feePercent)
        {
            const decimal minimumDisbursementFeeAmount = 10m;

            if (principal <= 0 || feePercent <= 0)
                return feePercent;

            var calculatedFeeAmount = principal * feePercent / 100m;
            return calculatedFeeAmount < minimumDisbursementFeeAmount
                ? (minimumDisbursementFeeAmount * 100m) / principal
                : feePercent;
        }

        private static decimal? ConvertRateToPercent(decimal? rate)
            => rate.HasValue ? rate.Value * 100m : null;

        // ─────────────────────────────────────────────────────────────────────
        // Private: helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task<CreditProductViewModel?> GetProductByCodeAsync(string? productCode)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return null;

            var normalized = productCode.Trim();
            var allProducts = await products.SelectAllAsync();
            return allProducts.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.ProductCode) &&
                string.Equals(p.ProductCode.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<CreditDefAmortizationTemplateViewModel?> GetTemplateByCodeAsync(string? templateCode)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
                return null;

            var normalized = templateCode.Trim();
            var allTemplates = await templates.SelectAllAsync();
            return allTemplates.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(t.TemplateCode) &&
                string.Equals(t.TemplateCode.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
        }

        private async Task ApplyTemplateIfNeededAsync(CreditCaseAmortPlanInputViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.SelectedTemplateCode) || !string.IsNullOrWhiteSpace(vm.TemplateName))
                return;

            var templateVm = await GetTemplateByCodeAsync(vm.SelectedTemplateCode);
            if (templateVm != null)
                ApplyTemplateTo(templateVm, vm);
        }

        private async Task<CreditCaseLoanApplicationViewModel?> ResolveSelectedCreditCaseAsync(CreditCaseAmortPlanInputViewModel vm)
        {
            vm.CreditCaseNumber = string.IsNullOrWhiteSpace(vm.CreditCaseNumber)
                ? null
                : vm.CreditCaseNumber.Trim();

            if (!vm.SelectedCreditCaseId.HasValue && vm.CreditCaseId.HasValue && vm.CreditCaseId.Value > 0)
                vm.SelectedCreditCaseId = vm.CreditCaseId.Value;

            // fallback by case number
            if ((!vm.SelectedCreditCaseId.HasValue || vm.SelectedCreditCaseId.Value <= 0) &&
                !string.IsNullOrWhiteSpace(vm.CreditCaseNumber))
            {
                var allCases = await creditCases.SelectAllAsync();
                var found = allCases.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.CaseNumber) &&
                    string.Equals(x.CaseNumber.Trim(), vm.CreditCaseNumber.Trim(), StringComparison.OrdinalIgnoreCase));

                if (found != null)
                    vm.SelectedCreditCaseId = found.CreditCaseId;
            }

            if (!vm.SelectedCreditCaseId.HasValue || vm.SelectedCreditCaseId.Value <= 0)
            {
                vm.CreditCaseId = null;
                return null;
            }

            var loanApplication = await LoadLoanApplicationAsync(vm.SelectedCreditCaseId.Value);
            if (loanApplication?.CreditCase == null)
            {
                vm.CreditCaseId = null;
                return null;
            }

            vm.CreditCaseId = loanApplication.CreditCase.CreditCaseId;
            vm.SelectedCreditCaseId = loanApplication.CreditCase.CreditCaseId;
            vm.SelectedBankPartyId = loanApplication.CreditCase.BankPartyId;
            vm.CreditCaseNumber = loanApplication.CreditCase.CaseNumber;

            return loanApplication;
        }

        //private async Task<CreditCaseLoanApplicationViewModel?> ResolveSelectedCreditCaseAsync(CreditCaseAmortPlanInputViewModel vm)
        //{
        //    vm.CreditCaseNumber = string.IsNullOrWhiteSpace(vm.CreditCaseNumber) ? null : vm.CreditCaseNumber.Trim();

        //    if (!vm.SelectedCreditCaseId.HasValue && vm.CreditCaseId.HasValue && vm.CreditCaseId.Value > 0)
        //        vm.SelectedCreditCaseId = vm.CreditCaseId.Value;

        //    if (!vm.SelectedCreditCaseId.HasValue || vm.SelectedCreditCaseId.Value <= 0)
        //    {
        //        vm.CreditCaseId = null;
        //        return null;
        //    }

        //    var loanApplication = await LoadLoanApplicationAsync(vm.SelectedCreditCaseId.Value);
        //    if (loanApplication?.CreditCase == null)
        //    {
        //        vm.CreditCaseId = null;
        //        return null;
        //    }

        //    vm.CreditCaseId = loanApplication.CreditCase.CreditCaseId;
        //    vm.SelectedCreditCaseId = loanApplication.CreditCase.CreditCaseId;
        //    vm.SelectedBankPartyId = loanApplication.CreditCase.BankPartyId;
        //    vm.CreditCaseNumber = loanApplication.CreditCase.CaseNumber;

        //    return loanApplication;
        //}

        private async Task<CreditCaseAmortPlanVersionViewModel?> LoadCurrentAmortPlanAsync(long? creditCaseId)
        {
            if (!creditCaseId.HasValue || creditCaseId.Value <= 0)
                return null;

            var loan = await creditCaseLoanRepository.SelectByIdAsync(creditCaseId.Value, HttpContext.RequestAborted);
            return loan?.CreditCaseAmortPlanVersion;
        }

        private async Task<CreditCaseLoanApplicationViewModel?> LoadLoanApplicationAsync(long creditCaseId)
        {
            if (creditCaseId <= 0)
                return null;

            try
            {
                return await creditCases.GetLoanApplicationByCaseIdAsync(creditCaseId);
            }
            catch
            {
                return null;
            }
        }

        private async Task ApplyCreditCaseDefaultsAsync(CreditCaseAmortPlanInputViewModel vm, bool overwriteEditableFields)
        {
            var loanApplication = await ResolveSelectedCreditCaseAsync(vm);
            if (loanApplication?.CreditCase == null)
            {
                vm.CreditCaseId = null;
                return;
            }

            ApplyCreditCaseDefaults(vm, loanApplication, overwriteEditableFields);
        }

        private static void ApplyExistingPlanTo(
            CreditCaseAmortPlanInputViewModel vm,
            CreditCaseAmortPlanVersionViewModel currentPlanVersion)
        {
            vm.SelectedTemplateCode = currentPlanVersion.AmortizationTemplateCode;
            vm.Principal = currentPlanVersion.PrincipalAmount;
            vm.BaseAnnualRate = currentPlanVersion.BaseInterestRate;
            vm.UseIndexedRate = currentPlanVersion.UseIndexedRate;

            if (!string.IsNullOrWhiteSpace(currentPlanVersion.IndexCode))
                vm.IndexCode = currentPlanVersion.IndexCode;

            if (currentPlanVersion.IndexRatePercent.HasValue)
                vm.IndexRatePercent = currentPlanVersion.IndexRatePercent.Value;

            vm.PrincipalCalcMethod = MapPrincipalCalculationMethod(
                currentPlanVersion.PrincipalCalculationMethodCode,
                null,
                vm.PrincipalCalcMethod);

            vm.FixedInstallmentAmount = currentPlanVersion.FixedInstallmentAmount;
            vm.FixedPrincipalAmount = currentPlanVersion.FixedPrincipalAmount;
            vm.FrequencyCode = MapFrequencyCode(currentPlanVersion.FrequencyCode, vm.FrequencyCode);
            vm.DueDayPolicy = MapDueDayPolicy(currentPlanVersion.DueDayPolicyCode, vm.DueDayPolicy);
            vm.FixedDayOfMonth = currentPlanVersion.FixedDayOfMonth ?? vm.FixedDayOfMonth;
            vm.NextInstallmentDate = currentPlanVersion.FirstDueDate?.ToDateTime(TimeOnly.MinValue);
            vm.DayCountMethod = MapDayCountMethod(currentPlanVersion.DayCountCalculationTypeCode, vm.DayCountMethod);
            vm.GraceType = MapGraceType(currentPlanVersion.GraceTypeCode, vm.GraceType);
            vm.GraceUntilDate = currentPlanVersion.GraceUntilDate?.ToDateTime(TimeOnly.MinValue);
            vm.AccrueInterestDuringFullGrace = currentPlanVersion.AccrueInterestDuringFullGrace ?? vm.AccrueInterestDuringFullGrace;
            vm.CapitalizeDuringFullGrace = currentPlanVersion.CapitalizeDuringFullGrace ?? vm.CapitalizeDuringFullGrace;
            vm.AddFinalBalloonAll = currentPlanVersion.AddFinalBalloonAll ?? vm.AddFinalBalloonAll;

            if (currentPlanVersion.TotalDisbursementFeePercent.HasValue)
                vm.TotalDisbursementFeePercent = currentPlanVersion.TotalDisbursementFeePercent.Value;

            vm.StartDate = currentPlanVersion.EffectiveFromDate.ToDateTime(TimeOnly.MinValue);

            var selectedEndDate = currentPlanVersion.EffectiveToDate
                ?? currentPlanVersion.MaturityDate
                ?? currentPlanVersion.EffectiveFromDate;

            vm.EndDate = selectedEndDate.ToDateTime(TimeOnly.MinValue);
            vm.TermMonths = CalculateMonthDifference(vm.StartDate, vm.EndDate);
            vm.GraceMonthsCount = currentPlanVersion.RequestedGracePeriodMonths;
            vm.DisbursementFees = new List<DisbursementFeeInput>();
        }

        private static void ApplyCreditCaseDefaults(
            CreditCaseAmortPlanInputViewModel vm,
            CreditCaseLoanApplicationViewModel loanApplication,
            bool overwriteEditableFields)
        {
            var creditCase = loanApplication.CreditCase;
            var loanDetails = loanApplication.LoanDetails;
            var selectedStartDate = ToDateTime(creditCase.StartDateApproved ?? creditCase.StartDateRequested);
            var selectedEndDate = ToDateTime(creditCase.MaturityDateApproved ?? creditCase.MaturityDateRequested);
            var selectedPrincipal = creditCase.CreditAmountApproved ?? creditCase.CreditAmountRequested;
            var selectedInterestRate = creditCase.InterestRateApproved ?? creditCase.InterestRateRequested;
            var selectedChargeRate = creditCase.PricingChargeRateApproved ?? creditCase.PricingChargeRateRequested;

            vm.CreditCaseId = creditCase.CreditCaseId;
            vm.SelectedCreditCaseId = creditCase.CreditCaseId;
            vm.SelectedBankPartyId = creditCase.BankPartyId;
            vm.CreditCaseNumber = creditCase.CaseNumber;

            if (string.IsNullOrWhiteSpace(vm.SelectedProductCode))
                vm.SelectedProductCode = creditCase.ProductCode;

            if (!overwriteEditableFields)
                return;

            vm.SelectedProductCode = creditCase.ProductCode;
            if (selectedPrincipal > 0)
                vm.Principal = selectedPrincipal;

            if (selectedInterestRate > 0)
                vm.BaseAnnualRate = selectedInterestRate;

            vm.StartDate = selectedStartDate;
            vm.EndDate = selectedEndDate;
            vm.NextInstallmentDate = null;
            vm.TermMonths = CalculateMonthDifference(selectedStartDate, selectedEndDate);
            vm.GraceMonthsCount = loanDetails?.RequestedGracePeriod;

            if (selectedChargeRate > 0)
                vm.TotalDisbursementFeePercent = selectedChargeRate;
        }

        private static object BuildCreditCaseDefaults(CreditCaseLoanApplicationViewModel loanApplication)
        {
            var creditCase = loanApplication.CreditCase;
            var startDate = ToDateTime(creditCase.StartDateApproved ?? creditCase.StartDateRequested);
            var endDate = ToDateTime(creditCase.MaturityDateApproved ?? creditCase.MaturityDateRequested);

            return new
            {
                bankPartyId = creditCase.BankPartyId,
                creditCaseId = creditCase.CreditCaseId,
                creditCaseNumber = creditCase.CaseNumber,
                selectedProductCode = creditCase.ProductCode,
                principal = creditCase.CreditAmountApproved ?? creditCase.CreditAmountRequested,
                baseAnnualRate = creditCase.InterestRateApproved ?? creditCase.InterestRateRequested,
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                nextInstallmentDate = ResolveFirstInstallmentDate(
                    startDate,
                    endDate,
                    null,
                    FrequencyCode.Monthly,
                    DueDayPolicy.EndOfMonth,
                    28,
                    null).ToString("yyyy-MM-dd"),
                termMonths = CalculateMonthDifference(startDate, endDate),
                graceMonthsCount = loanApplication.LoanDetails?.RequestedGracePeriod,
                totalDisbursementFeePercent = creditCase.PricingChargeRateApproved ?? creditCase.PricingChargeRateRequested
            };
        }

        private static DateTime ToDateTime(DateOnly date)
            => date.ToDateTime(TimeOnly.MinValue);

        private async Task PopulateBankPartyOptionsAsync(CreditCaseAmortPlanInputViewModel vm, int? preselect = null)
        {
            var list = await bankParties.SelectAllActiveForDropdownAsync();
            var opts = list
                .OrderBy(x => x.FullName)
                .Select(x =>
                {
                    var label = string.IsNullOrWhiteSpace(x.PartyCode)
                        ? x.FullName
                        : $"{x.FullName} ({x.PartyCode})";
                    return new KeyValuePair<string, string>(x.BankPartyId.ToString(), label);
                })
                .ToList();

            opts.Insert(0, new KeyValuePair<string, string>(string.Empty, "-- Select client --"));
            vm.BankPartyOptions = opts;
            vm.SelectedBankPartyId ??= preselect;
        }

        private async Task PopulateCreditCaseOptionsAsync(
            CreditCaseAmortPlanInputViewModel vm,
            int? bankPartyId,
            long? preselect = null)
        {
            var opts = new List<KeyValuePair<string, string>>
            {
                new(string.Empty, "-- Select credit case --")
            };

            if (bankPartyId.HasValue && bankPartyId.Value > 0)
            {
                var list = await creditCases.SelectForm31ClientCreditHUB(bankPartyId.Value);
                opts.AddRange(list
                    .Where(x => string.Equals(x.ApplicationType, "Loan", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.CreditCaseId)
                    .Select(x => new KeyValuePair<string, string>(
                        x.CreditCaseId.ToString(),
                        $"{x.CaseNumber} | {x.ProductDisplayName} | {x.Status}")));
            }

            vm.CreditCaseOptions = opts;
            vm.SelectedCreditCaseId ??= preselect;
        }

        private static int CalculateMonthDifference(DateTime startDate, DateTime endDate)
        {
            var diff = (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month;
            return Math.Max(1, diff == 0 && endDate.Date > startDate.Date ? 1 : diff);
        }

        private async Task PopulateProductOptionsAsync(CreditCaseAmortPlanInputViewModel vm, string? preselect = null)
        {
            var list = await products.SelectAllAsync();
            var opts = list.Where(p => p.IsActive && (p.IsApproved ?? false))
                           .OrderBy(p => p.ProductName)
                           .Select(p => new KeyValuePair<string, string>(p.ProductCode, p.ProductName))
                           .ToList();
            vm.ProductOptions = opts;
            if (opts.Count > 0)
                vm.SelectedProductCode ??= preselect ?? opts[0].Key;
        }

        private async Task PopulateTemplateOptionsAsync(CreditCaseAmortPlanInputViewModel vm, string? preselect = null)
        {
            var list = await templates.SelectAllAsync();
            var opts = list.Where(t => t.IsActive && (t.IsApproved ?? false))
                           .OrderBy(t => t.TemplateName)
                           .Select(t => new KeyValuePair<string, string>(t.TemplateCode, t.TemplateName))
                           .ToList();

            opts.Insert(0, new KeyValuePair<string, string>("", "(No Template – Use Custom Parameters)"));

            vm.TemplateOptions = opts;
            vm.SelectedTemplateCode = preselect;
        }

        private static void ApplyProductTo(CreditProductViewModel p, CreditCaseAmortPlanInputViewModel vm)
        {
            vm.ProductAmountMin = p.AmountMin;
            vm.ProductAmountMax = p.AmountMax;

            if (vm.Principal <= 0 && p.AmountMin.HasValue)
                vm.Principal = p.AmountMin.Value;
        }

        private static void ApplyTemplateTo(CreditDefAmortizationTemplateViewModel t, CreditCaseAmortPlanInputViewModel vm)
        {
            vm.TemplateName = t.TemplateName;
            vm.TemplateDescription = t.TemplateDescription;
            vm.TemplateAmortizationMethodCode = t.AmortizationMethodCode;
            vm.TemplatePaymentMethodCode = t.PaymentMethodCode;
            vm.InterestPostingFrequencyCode = t.InterestPostingFrequencyselectCode;
            vm.AllowSeasonal = t.AllowSeasonal;
            vm.InstallmentMonthsMask = NormalizeMonthMask(t.InstallmentMonthsMask);
            vm.GraceMonthsMaskPrincipal = NormalizeMonthMask(t.GraceMonthsMaskPrincipal);
            vm.GraceMonthsMaskInterest = NormalizeMonthMask(t.GraceMonthsMaskInterest);
            vm.GraceMonthsCount = ResolveGraceMonthsCount(t);
            vm.RateTypeCode = t.RateTypeCode;
            vm.RateResetFrequency = t.RateResetFrequency;
            vm.RateMarginBps = t.RateMarginBps;
            vm.LastInstallmentAdjustCode = t.LastInstallmentAdjustCode;

            vm.FrequencyCode = MapFrequencyCode(t.FrequencyCode, vm.FrequencyCode);
            vm.DueDayPolicy = MapDueDayPolicy(t.DueDayPolicyCode, vm.DueDayPolicy);
            vm.FixedDayOfMonth = t.FixedDayOfMonth ?? vm.FixedDayOfMonth;
            vm.DayCountMethod = MapDayCountMethod(t.DayCountCalculationTypeCode, vm.DayCountMethod);
            vm.PrincipalCalcMethod = MapPrincipalCalculationMethod(
                t.PrincipalMethodCode,
                t.PaymentMethodCode,
                vm.PrincipalCalcMethod);
            vm.CurrencyDecimals = t.CurrencyDecimals > 0 ? t.CurrencyDecimals : vm.CurrencyDecimals;
            vm.RoundingMode = MapRoundingMode(t.RoundingModeCode, vm.RoundingMode);
            vm.GraceType = MapGraceType(t.GraceTypeCode, vm.GraceType);
            vm.AccrueInterestDuringFullGrace = t.AccrueInterestDuringGrace;
            vm.CapitalizeDuringFullGrace = t.CapitalizeInterestOnGraceEnd;
            vm.AddFinalBalloonAll = IsBalloonAtMaturity(t.BalloonPolicyCode);
            vm.UseIndexedRate = IsIndexedRate(t.RateTypeCode);

            if (!string.IsNullOrWhiteSpace(t.IndexCode))
                vm.IndexCode = t.IndexCode;

            if (t.RateMarginBps.HasValue)
                vm.BaseAnnualRate = t.RateMarginBps.Value / 100m;

            if (vm.PrincipalCalcMethod != PrincipalCalculationMethod.FixedInstallment)
                vm.FixedInstallmentAmount = null;

            if (vm.PrincipalCalcMethod != PrincipalCalculationMethod.FixedPrincipal)
                vm.FixedPrincipalAmount = null;

            vm.GraceUntilDate = vm.GraceMonthsCount.HasValue && vm.GraceMonthsCount.Value > 0
                ? CalculateGraceUntilDate(vm.StartDate, vm.GraceMonthsCount.Value, vm)
                : null;
        }

        private static void NormalizeDerivedFields(CreditCaseAmortPlanInputViewModel vm)
        {
            vm.EndDate = CalcEndDate(vm.StartDate, vm.TermMonths);

            var nextInstallmentDate = vm.NextInstallmentDate;
            var dueDayPolicy = vm.DueDayPolicy;
            var fixedDayOfMonth = vm.FixedDayOfMonth;
            NormalizeScheduleAnchor(
                vm.StartDate,
                vm.EndDate,
                ref nextInstallmentDate,
                vm.FrequencyCode,
                ref dueDayPolicy,
                ref fixedDayOfMonth,
                vm.InstallmentMonthsMask);
            vm.NextInstallmentDate = nextInstallmentDate;
            vm.DueDayPolicy = dueDayPolicy;
            vm.FixedDayOfMonth = fixedDayOfMonth;

            if (vm.GraceType == GraceType.None)
            {
                vm.GraceUntilDate = null;
                return;
            }

            if (!vm.GraceUntilDate.HasValue && vm.GraceMonthsCount.HasValue && vm.GraceMonthsCount.Value > 0)
                vm.GraceUntilDate = ResolveGraceUntilDateFromSchedule(
                    vm.StartDate,
                    vm.EndDate,
                    vm.NextInstallmentDate,
                    vm.GraceMonthsCount.Value,
                    vm.FrequencyCode,
                    vm.DueDayPolicy,
                    vm.FixedDayOfMonth,
                    vm.InstallmentMonthsMask);
        }

        private static DateTime CalcEndDate(DateTime start, int termMonths) => start.AddMonths(termMonths);

        private static List<DateTime> BuildScheduleDates(CreditCaseAmortPlanInputViewModel vm)
            => BuildScheduleDates(
                vm.StartDate,
                vm.EndDate,
                vm.NextInstallmentDate,
                vm.FrequencyCode,
                vm.DueDayPolicy,
                vm.FixedDayOfMonth,
                vm.InstallmentMonthsMask);

        private static List<DateTime> BuildScheduleDates(
            DateTime startDate,
            DateTime endDate,
            DateTime? nextInstallmentDate,
            FrequencyCode frequencyCode,
            DueDayPolicy dueDayPolicy,
            int fixedDayOfMonth,
            string? installmentMonthsMask)
        {
            if (frequencyCode == FrequencyCode.AtMaturity)
                return new List<DateTime> { endDate.Date };

            var firstDueDate = ResolveFirstInstallmentDate(
                startDate,
                endDate,
                nextInstallmentDate,
                frequencyCode,
                dueDayPolicy,
                fixedDayOfMonth,
                installmentMonthsMask);

            if (HasInstallmentMask(installmentMonthsMask))
                return BuildMaskedMonthlySchedule(startDate, endDate, firstDueDate, dueDayPolicy, fixedDayOfMonth, installmentMonthsMask);

            if (TryGetDayInterval(frequencyCode, out var intervalDays))
                return BuildDayBasedSchedule(startDate, endDate, firstDueDate, intervalDays);

            if (TryGetMonthInterval(frequencyCode, out var intervalMonths))
                return BuildMonthBasedSchedule(startDate, endDate, firstDueDate, intervalMonths, dueDayPolicy, fixedDayOfMonth);

            if (frequencyCode == FrequencyCode.Custom)
                throw new InvalidOperationException("Custom frequency is not supported by the amortization plan generator.");

            return BuildMonthBasedSchedule(startDate, endDate, firstDueDate, 1, dueDayPolicy, fixedDayOfMonth);
        }

        private static List<DateTime> BuildDayBasedSchedule(DateTime startDate, DateTime endDate, DateTime firstDueDate, int intervalDays)
        {
            var dates = new List<DateTime>();
            if (firstDueDate > startDate.Date && firstDueDate < endDate.Date)
                dates.Add(firstDueDate.Date);

            var cursor = firstDueDate.Date;

            while (true)
            {
                cursor = cursor.AddDays(intervalDays);
                if (cursor >= endDate.Date)
                    break;

                if (cursor > firstDueDate.Date)
                    dates.Add(cursor);
            }

            AppendMaturityDate(dates, endDate.Date);
            return dates;
        }

        private static List<DateTime> BuildMonthBasedSchedule(
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

        private static List<DateTime> BuildMaskedMonthlySchedule(
            DateTime startDate,
            DateTime endDate,
            DateTime firstDueDate,
            DueDayPolicy dueDayPolicy,
            int fixedDayOfMonth,
            string? installmentMonthsMask)
        {
            var dates = new List<DateTime>();
            var mask = NormalizeMonthMask(installmentMonthsMask);

            if (string.IsNullOrWhiteSpace(mask))
                return BuildMonthBasedSchedule(startDate, endDate, firstDueDate, 1, dueDayPolicy, fixedDayOfMonth);

            if (firstDueDate > startDate.Date && firstDueDate < endDate.Date)
                dates.Add(firstDueDate.Date);

            var cursor = firstDueDate.Date;
            while (true)
            {
                cursor = cursor.AddMonths(1);
                var due = ApplyDueDayPolicy(cursor, dueDayPolicy, fixedDayOfMonth);
                if (due >= endDate.Date)
                    break;

                if (due > firstDueDate.Date && IsMonthEnabled(mask, due.Month))
                    dates.Add(due);
            }

            AppendMaturityDate(dates, endDate.Date);
            return dates;
        }

        private static void AppendMaturityDate(List<DateTime> dates, DateTime maturityDate)
        {
            if (dates.Count == 0 || dates[^1].Date != maturityDate.Date)
                dates.Add(maturityDate.Date);
        }

        private static DateTime ApplyDueDayPolicy(DateTime anchor, DueDayPolicy policy, int fixedDayOfMonth)
        {
            return policy == DueDayPolicy.EndOfMonth
                ? new DateTime(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month))
                : new DateTime(anchor.Year, anchor.Month,
                    Math.Clamp(fixedDayOfMonth, 1, DateTime.DaysInMonth(anchor.Year, anchor.Month)));
        }

        private static void NormalizeScheduleAnchor(
            DateTime startDate,
            DateTime endDate,
            ref DateTime? nextInstallmentDate,
            FrequencyCode frequencyCode,
            ref DueDayPolicy dueDayPolicy,
            ref int fixedDayOfMonth,
            string? installmentMonthsMask)
        {
            fixedDayOfMonth = Math.Clamp(fixedDayOfMonth, 1, 31);

            if (nextInstallmentDate.HasValue && nextInstallmentDate.Value.Date <= startDate.Date)
                nextInstallmentDate = null;

            nextInstallmentDate ??= ResolveFirstInstallmentDate(
                startDate,
                endDate,
                null,
                frequencyCode,
                dueDayPolicy,
                fixedDayOfMonth,
                installmentMonthsMask);

            if (!nextInstallmentDate.HasValue)
                return;

            var nextDue = nextInstallmentDate.Value.Date;
            fixedDayOfMonth = Math.Clamp(nextDue.Day, 1, 31);
            dueDayPolicy = IsLastDayOfMonth(nextDue)
                ? DueDayPolicy.EndOfMonth
                : DueDayPolicy.FixedDayOfMonth;
        }

        private static DateTime? CalculateGraceUntilDate(DateTime startDate, int graceMonthsCount, CreditCaseAmortPlanInputViewModel vm)
            => ResolveGraceUntilDateFromSchedule(
                startDate,
                vm.EndDate,
                vm.NextInstallmentDate,
                graceMonthsCount,
                vm.FrequencyCode,
                vm.DueDayPolicy,
                vm.FixedDayOfMonth,
                vm.InstallmentMonthsMask);

        private static DateTime? ResolveGraceUntilDateFromSchedule(
            DateTime startDate,
            DateTime endDate,
            DateTime? nextInstallmentDate,
            int graceMonthsCount,
            FrequencyCode frequencyCode,
            DueDayPolicy dueDayPolicy,
            int fixedDayOfMonth,
            string? installmentMonthsMask)
        {
            if (graceMonthsCount <= 0)
                return null;

            var scheduleDates = BuildScheduleDates(
                startDate,
                endDate,
                nextInstallmentDate,
                frequencyCode,
                dueDayPolicy,
                fixedDayOfMonth,
                installmentMonthsMask);

            if (scheduleDates.Count == 0)
                return null;

            var graceIndex = Math.Min(Math.Max(graceMonthsCount - 1, 0), scheduleDates.Count - 1);
            return scheduleDates[graceIndex];
        }

        private static bool IsMonthBasedFrequency(FrequencyCode frequencyCode)
        {
            return frequencyCode is FrequencyCode.Monthly
                or FrequencyCode.Every2Months
                or FrequencyCode.Quarterly
                or FrequencyCode.Every4Months
                or FrequencyCode.SemiAnnually
                or FrequencyCode.Annually;
        }

        private static bool HasInstallmentMask(CreditCaseAmortPlanInputViewModel vm)
            => !string.IsNullOrWhiteSpace(NormalizeMonthMask(vm.InstallmentMonthsMask));

        private static bool HasInstallmentMask(string? installmentMonthsMask)
            => !string.IsNullOrWhiteSpace(NormalizeMonthMask(installmentMonthsMask));

        private static DateTime ResolveFirstInstallmentDate(
            DateTime startDate,
            DateTime endDate,
            DateTime? nextInstallmentDate,
            FrequencyCode frequencyCode,
            DueDayPolicy dueDayPolicy,
            int fixedDayOfMonth,
            string? installmentMonthsMask)
        {
            if (nextInstallmentDate.HasValue && nextInstallmentDate.Value.Date > startDate.Date)
                return nextInstallmentDate.Value.Date > endDate.Date ? endDate.Date : nextInstallmentDate.Value.Date;

            if (HasInstallmentMask(installmentMonthsMask))
            {
                var mask = NormalizeMonthMask(installmentMonthsMask);
                var cursor = startDate.Date;
                while (true)
                {
                    cursor = cursor.AddMonths(1);
                    var due = ApplyDueDayPolicy(cursor, dueDayPolicy, fixedDayOfMonth);
                    if (due >= endDate.Date)
                        return endDate.Date;

                    if (mask != null && IsMonthEnabled(mask, due.Month))
                        return due;
                }
            }

            if (TryGetDayInterval(frequencyCode, out var intervalDays))
            {
                var due = startDate.Date.AddDays(intervalDays);
                return due >= endDate.Date ? endDate.Date : due;
            }

            if (TryGetMonthInterval(frequencyCode, out var intervalMonths))
            {
                var due = ApplyDueDayPolicy(startDate.Date.AddMonths(intervalMonths), dueDayPolicy, fixedDayOfMonth);
                return due >= endDate.Date ? endDate.Date : due;
            }

            return endDate.Date;
        }

        private static bool TryGetDayInterval(FrequencyCode frequencyCode, out int intervalDays)
        {
            intervalDays = frequencyCode switch
            {
                FrequencyCode.Daily => 1,
                FrequencyCode.Weekly => 7,
                FrequencyCode.BiWeekly => 14,
                FrequencyCode.Every15Days => 15,
                FrequencyCode.Every28Days => 28,
                FrequencyCode.Every30Days => 30,
                FrequencyCode.Every45Days => 45,
                _ => 0
            };

            return intervalDays > 0;
        }

        private static bool TryGetMonthInterval(FrequencyCode frequencyCode, out int intervalMonths)
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

        private static bool IsLastDayOfMonth(DateTime date)
            => date.Day == DateTime.DaysInMonth(date.Year, date.Month);

        private static bool IsMonthEnabled(string mask, int month)
            => month >= 1 && month <= 12 && mask[month - 1] == '1';

        private static string? NormalizeMonthMask(string? mask)
        {
            var normalized = string.IsNullOrWhiteSpace(mask) ? null : mask.Trim();
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 12 || normalized.Any(c => c is not '0' and not '1'))
                return null;

            return normalized;
        }

        private static int? ResolveGraceMonthsCount(CreditDefAmortizationTemplateViewModel template)
        {
            if (template.GraceMonthsCount.HasValue && template.GraceMonthsCount.Value > 0)
                return template.GraceMonthsCount.Value;

            var fromPrincipalMask = CountLeadingEnabledMonths(template.GraceMonthsMaskPrincipal);
            var fromInterestMask = CountLeadingEnabledMonths(template.GraceMonthsMaskInterest);
            var resolved = Math.Max(fromPrincipalMask, fromInterestMask);
            return resolved > 0 ? resolved : null;
        }

        private static int CountLeadingEnabledMonths(string? mask)
        {
            var normalized = NormalizeMonthMask(mask);
            if (string.IsNullOrWhiteSpace(normalized))
                return 0;

            var count = 0;
            foreach (var ch in normalized)
            {
                if (ch != '1')
                    break;

                count++;
            }

            return count;
        }

        private static FrequencyCode MapFrequencyCode(string? value, FrequencyCode fallback)
            => Enum.TryParse<FrequencyCode>(value, true, out var parsed) ? parsed : fallback;

        private static DueDayPolicy MapDueDayPolicy(string? value, DueDayPolicy fallback)
            => Enum.TryParse<DueDayPolicy>(value, true, out var parsed) ? parsed : fallback;

        private static GraceType MapGraceType(string? value, GraceType fallback)
            => Enum.TryParse<GraceType>(value, true, out var parsed) ? parsed : fallback;

        private static RoundingMode MapRoundingMode(string? value, RoundingMode fallback)
            => Enum.TryParse<RoundingMode>(value, true, out var parsed) ? parsed : fallback;

        private static bool IsBalloonAtMaturity(string? balloonPolicyCode)
            => string.Equals(balloonPolicyCode, nameof(BalloonPolicy.AtMaturityAll), StringComparison.OrdinalIgnoreCase);

        private static bool IsIndexedRate(string? rateTypeCode)
            => string.Equals(rateTypeCode, "Indexed", StringComparison.OrdinalIgnoreCase);

        private static EnumDayCountCalculationType MapDayCountMethod(
            string? templateValue,
            EnumDayCountCalculationType fallback)
        {
            if (string.IsNullOrWhiteSpace(templateValue))
                return fallback;

            var normalized = templateValue.Trim().ToUpperInvariant();

            return normalized switch
            {
                "ACTACT_ISDA" => EnumDayCountCalculationType.ActualActual_365_366,
               // "ACTUAL360" => EnumDayCountCalculationType.Actual360,
               // "ACTUAL365" => EnumDayCountCalculationType.Actual365,
               // "SIMPLE365" => EnumDayCountCalculationType.Actual365,
               // "THIRTY360" => EnumDayCountCalculationType.Thirty360E,
               // "THIRTY360E" => EnumDayCountCalculationType.Thirty360E,
                _ => Enum.TryParse<EnumDayCountCalculationType>(templateValue, true, out var parsed) ? parsed : fallback
            };
        }

        private static PrincipalCalculationMethod MapPrincipalCalculationMethod(
            string? principalMethodCode,
            string? paymentMethodCode,
            PrincipalCalculationMethod fallback)
        {
            if (!string.IsNullOrWhiteSpace(principalMethodCode))
            {
                switch (principalMethodCode.Trim().ToUpperInvariant())
                {
                    case "ANNUITY":
                        return PrincipalCalculationMethod.Annuity;
                    case "EQUALPRINCIPAL":
                        return PrincipalCalculationMethod.FixedPrincipal;
                    case "BULLET":
                        return PrincipalCalculationMethod.Bullet;
                }
            }

            if (string.Equals(paymentMethodCode, "FixedTotal", StringComparison.OrdinalIgnoreCase))
                return PrincipalCalculationMethod.FixedInstallment;

            return fallback;
        }

        private ContextSessionDto BuildContextSession()
        {
            var userIdRaw = User?.FindFirstValue("UserId") ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdRaw, out var userId);
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            return new ContextSessionDto
            {
                UserId        = userId,
                Username      = User?.Identity?.Name ?? "unknown",
                LanguageCode  = "en-US",
                FormId        = 33,
                FormSessionId = string.IsNullOrWhiteSpace(formSessionId) ? Guid.NewGuid().ToString() : formSessionId,
                IsAutoApprove = false,
                Permissions   = FormPermissions.Insert
            };
        }
    }
}
