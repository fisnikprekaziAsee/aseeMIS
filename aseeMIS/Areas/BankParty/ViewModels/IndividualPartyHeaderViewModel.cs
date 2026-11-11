namespace aseeMIS.Areas.BankParty.ViewModels
{
    public class IndividualPartyHeaderViewModel
    {
        // Identity
        public string? ClientId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; } // if set, used for the title

        // Residency / country chips
        public string? ResidencyStatus { get; set; } // Resident / Non-Resident
        public string? Country { get; set; }  // e.g., "United States"

        // Contact
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        // Account info
        public string? PersonalId { get; set; } // e.g., SSN / National ID
        public string? AccountNumber { get; set; } // e.g., IBAN / ACC
        public string? BranchName { get; set; }

        // Important dates
        public string? NextRevisionDate { get; set; } // e.g., "2024-12-15"
        public string? PassportExpiryDate { get; set; } // e.g., "2026-03-20"

        // Media
        public string? ProfileImageUrl { get; set; }
        public string? SignatureImageUrl { get; set; }
    }
}
