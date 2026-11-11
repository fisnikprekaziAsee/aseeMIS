using dsBankParty.Core.ViewModels;
using dsCredit.Core.DTOs.FN;

namespace aseeMIS.Areas.Credit.ViewModels
{
    public class CreditBankPartyProfileViewModel
    {
        public CreditBankPartyProfileDto? Profile { get; set; }
        public BankPartyViewModel? BankParty { get; set; }
        public IReadOnlyList<CreditCaseProductByBankPartyDto> ActiveProducts { get; set; } = Array.Empty<CreditCaseProductByBankPartyDto>();

        public bool HasProfile => Profile != null;
        public bool IsIndividual => Profile?.IsIndividual ?? BankParty?.IsIndividual ?? true;
        public int BankPartyId => Profile?.BankPartyId ?? BankParty?.BankPartyId ?? 0;
        public string FullName => FirstNonEmpty(Profile?.FullName, BankParty?.FullName, Profile?.PartyLegalName, "Unknown customer");
        public string PartyCode => FirstNonEmpty(Profile?.PartyCode, BankParty?.PartyCode, "-");
        public string IdentificationNumber => FirstNonEmpty(Profile?.IdentificationNumber, BankParty?.IdentificationNumber, "-");
        public string SegmentName => FirstNonEmpty(BankParty?.PartySegmentName, Profile?.PartySegmentId > 0 ? $"Segment {Profile.PartySegmentId}" : null, "Standard");
        public string RiskLabel => FirstNonEmpty(Profile?.RiskGroupCode, BankParty?.RiskGroupCode, "Low Risk");
        public string Email => FirstNonEmpty(Profile?.ContactEmail, BankParty?.ContactEmail, "-");
        public string Phone => FirstNonEmpty(Profile?.PhoneNumber, BankParty?.PhoneNumber, "-");
        public string Address => FirstNonEmpty(Profile?.ContactAddress, BankParty?.ContactAddress, "-");
        public string Municipality => FirstNonEmpty(Profile?.ContactMunicipalityName, BankParty?.ContactMunicipalityName, string.Empty);
        public string PartyTypeLabel => IsIndividual ? "Individual" : "Legal Entity";
        public string PrimaryDescriptor => IsIndividual
            ? FirstNonEmpty(Profile?.JobTitle, Profile?.EmployerName, Profile?.PositionLevelTypeCode, "Personal Client")
            : FirstNonEmpty(Profile?.NaceCountryCode, Profile?.PartyDescription, "Business Client");
        public string RepresentativeName => FirstNonEmpty(Profile?.LegalRepresentativeName, "-");
        public string PreferredCurrency => ActiveProducts.FirstOrDefault()?.CurrencyCode ?? "EUR";
        public int Open360FormId => IsIndividual ? 102 : 103;

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }
    }
}
