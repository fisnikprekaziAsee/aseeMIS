namespace aseeMIS.Areas.BankParty.ViewModels
{
    /// <summary>Lightweight DTO used by BankParty context header partials inside the widget.</summary>
    public class BankPartyContextDto
    {
        public int BankPartyId { get; set; }
        public string FullName { get; set; } = "";
        public string PartyCode { get; set; } = "";
        public string? IdentificationNumber { get; set; }
        public bool IsIndividual { get; set; }
        public bool IsResident { get; set; }
        public string? MunicipalityName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }
}
