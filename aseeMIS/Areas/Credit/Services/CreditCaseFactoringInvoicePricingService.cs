using dsCredit.Core.Services.Interfaces;
using dsCreditEngine.Engine;
using dsCreditPriceList.Core.Services.Interfaces;
using aseeMIS.Areas.Credit.ViewModels;

namespace aseeMIS.Areas.Credit.Services
{
    public interface ICreditCaseFactoringInvoicePricingService
    {
        Task<FactoringInvoicePricingContextViewModel?> GetPricingContextAsync(
            long creditCaseId,
            CancellationToken cancellationToken = default);

        Task<FactoringInvoicePricingResultViewModel?> CalculateAsync(
            long creditCaseId,
            decimal invoiceAmount,
            decimal? requestedAmount,
            decimal? dilutionAmount,
            DateOnly? disbursementDate,
            DateOnly bankDueDate,
            CancellationToken cancellationToken = default);
    }

    public class CreditCaseFactoringInvoicePricingService(
        ICreditCaseService creditCaseService,
        ICreditCaseFactoringService factoringService,
        ICreditPriceListItemFactoringService priceListItemFactoringService,
        dsBankParty.Core.Services.Interfaces.IBankPartyService<dsBankParty.Core.ViewModels.BankPartyViewModel> bankPartyService)
        : ICreditCaseFactoringInvoicePricingService
    {
        public async Task<FactoringInvoicePricingContextViewModel?> GetPricingContextAsync(
            long creditCaseId,
            CancellationToken cancellationToken = default)
        {
            if (creditCaseId <= 0)
                return null;

            var creditCase = await creditCaseService.GetByIdAsync(creditCaseId);
            if (creditCase.CreditCaseId <= 0)
                return null;

            var factoring = await factoringService.GetByIdAsync(creditCaseId);
            var advanceRatePercent = factoring.ApprovedAdvanceRatePercent ?? 100m;
            var retentionRatePercent = factoring.ApprovedRetentionRatePercent
                ?? Math.Max(0m, 100m - advanceRatePercent);
            var caseOwnerIsIndividual = false;
            var caseOwnerBankPartyId = creditCase.BankPartyId;

            if (caseOwnerBankPartyId > 0)
            {
                var ownerParty = await bankPartyService.GetByIdAsync(caseOwnerBankPartyId);
                if (ownerParty.BankPartyId > 0)
                    caseOwnerIsIndividual = ownerParty.IsIndividual;
            }

            if (string.IsNullOrWhiteSpace(creditCase.ProductCode)
                || !creditCase.PriceListId.HasValue
                || creditCase.PriceListId.Value <= 0)
            {
                return new FactoringInvoicePricingContextViewModel
                {
                    ProductCode = creditCase.ProductCode ?? string.Empty,
                    PriceListId = creditCase.PriceListId,
                    AdvanceRatePercent = advanceRatePercent,
                    RetentionRatePercent = retentionRatePercent,
                    DilutionRatePercent = factoring.ApprovedDilutionReservePercent ?? 0m,
                    CaseOwnerBankPartyId = caseOwnerBankPartyId,
                    CaseOwnerIsIndividual = caseOwnerIsIndividual,
                    Message = "Credit case nuk ka ProductCode ose PriceListId të caktuar."
                };
            }

            var priceListItems = await priceListItemFactoringService.SelectFactoringPriceListByProductAsync(
                creditCase.ProductCode,
                creditCase.PriceListId.Value);

            return new FactoringInvoicePricingContextViewModel
            {
                ProductCode = creditCase.ProductCode,
                PriceListId = creditCase.PriceListId,
                AdvanceRatePercent = advanceRatePercent,
                RetentionRatePercent = retentionRatePercent,
                DilutionRatePercent = factoring.ApprovedDilutionReservePercent ?? 0m,
                CaseOwnerBankPartyId = caseOwnerBankPartyId,
                CaseOwnerIsIndividual = caseOwnerIsIndividual,
                PriceListItems = priceListItems,
                Message = priceListItems.Count == 0
                    ? $"Nuk u gjet pricelist aktiv për produktin {creditCase.ProductCode}."
                    : null
            };
        }

        public async Task<FactoringInvoicePricingResultViewModel?> CalculateAsync(
            long creditCaseId,
            decimal invoiceAmount,
            decimal? requestedAmount,
            decimal? dilutionAmount,
            DateOnly? disbursementDate,
            DateOnly bankDueDate,
            CancellationToken cancellationToken = default)
        {
            var effectiveDisbursementDate = disbursementDate ?? DateOnly.FromDateTime(DateTime.Today);

            if (creditCaseId <= 0 || invoiceAmount <= 0m || bankDueDate <= effectiveDisbursementDate)
                return null;

            if (requestedAmount.GetValueOrDefault() > invoiceAmount)
                return null;

            var creditCase = await creditCaseService.GetByIdAsync(creditCaseId);
            if (creditCase.CreditCaseId <= 0
                || string.IsNullOrWhiteSpace(creditCase.ProductCode)
                || !creditCase.PriceListId.HasValue
                || creditCase.PriceListId.Value <= 0)
            {
                return null;
            }

            var factoring = await factoringService.GetByIdAsync(creditCaseId);
            var dilutionRatePercent = factoring.ApprovedDilutionReservePercent ?? 0m;
            var dilution = dilutionAmount.HasValue && dilutionAmount.Value >= 0m
                ? RoundMoney(Math.Min(dilutionAmount.Value, invoiceAmount))
                : RoundMoney(invoiceAmount * dilutionRatePercent / 100m);

            var eligible = RoundMoney(Math.Max(0m, invoiceAmount - dilution));

            var requested = requestedAmount.GetValueOrDefault();
            if (requested <= 0m)
                requested = eligible > 0m ? eligible : invoiceAmount;

            if (requested > invoiceAmount)
                return null;

            var approved = RoundMoney(Math.Min(requested, eligible));

            var advanceRatePercent = factoring.ApprovedAdvanceRatePercent ?? 100m;
            var retentionRatePercent = factoring.ApprovedRetentionRatePercent
                ?? Math.Max(0m, 100m - advanceRatePercent);
            var retention = RoundMoney(approved * retentionRatePercent / 100m);
            var advance = RoundMoney(Math.Max(0m, approved - retention));

            if (advance <= 0m)
                return null;

            var termMonths = FactoringInvoiceDiscountCalculator.CalculateTermMonths(effectiveDisbursementDate, bankDueDate);
            var priceListItem = await priceListItemFactoringService.SelectMatchingFactoringPriceListItemAsync(
                creditCase.ProductCode,
                creditCase.PriceListId.Value,
                advance,
                termMonths);

            if (priceListItem == null)
                return null;

            var discountAmount = FactoringInvoiceDiscountCalculator.CalculateDiscountAmount(
                advance,
                priceListItem.FactoringNominalInterestRate,
                effectiveDisbursementDate,
                bankDueDate);
            var commissionAmount = RoundMoney(priceListItem.FactoringCommissionFixedAmount);
            var netDisbursement = RoundMoney(advance - discountAmount - commissionAmount);

            return new FactoringInvoicePricingResultViewModel
            {
                FactoringNominalInterestRate = priceListItem.FactoringNominalInterestRate,
                FactoringCommissionFixedAmount = priceListItem.FactoringCommissionFixedAmount,
                AdvanceRatePercent = advanceRatePercent,
                RetentionRatePercent = retentionRatePercent,
                DilutionRatePercent = invoiceAmount > 0m
                    ? RoundMoney(dilution / invoiceAmount * 100m)
                    : dilutionRatePercent,
                DilutionAmount = dilution,
                EligibleAmount = eligible,
                ApprovedAmount = approved,
                AdvanceAmount = advance,
                RetentionAmount = retention,
                DiscountRatePercent = priceListItem.FactoringNominalInterestRate,
                DiscountAmount = discountAmount,
                CommissionAmount = commissionAmount,
                NetDisbursementAmount = netDisbursement,
                AmountFrom = priceListItem.AmountFrom,
                AmountTo = priceListItem.AmountTo,
                DaysOutstanding = FactoringInvoiceDiscountCalculator.CalculateDaysOutstanding(effectiveDisbursementDate, bankDueDate),
                TermMonths = termMonths
            };
        }

        private static decimal RoundMoney(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
