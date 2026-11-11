namespace aseeMIS.Areas.BankParty.ViewModels
{
    public class BankClientSectionViewModel
    {
        public class AccountViewModel
        {
            public string Name { get; set; } = "";
            public string Number { get; set; } = "";
            public decimal Balance { get; set; }
            public bool IsActive { get; set; }
        }

        public class CardViewModel
        {
            public string Type { get; set; } = "";
            public string Number { get; set; } = "";
            public decimal Limit { get; set; }
            public decimal Balance { get; set; }
            public string Expires { get; set; } = "";
            public bool IsActive { get; set; }
        }

        public class LoanViewModel
        {
            public string Type { get; set; } = "";
            public string Id { get; set; } = "";
            public decimal Amount { get; set; }
            public decimal Balance { get; set; }
            public decimal Rate { get; set; }
            public decimal Monthly { get; set; }
            public bool IsActive { get; set; }
        }

        // NEW: top summary boxes
        public class AccountSummaryViewModel
        {
            public decimal TotalBalance { get; set; }
            public decimal MonthlyTurnover { get; set; }
            public int TransactionsThisMonth { get; set; }
            public decimal AverageTransaction { get; set; }
            public decimal LargestTransaction { get; set; }
        }

        public class CreditSummaryViewModel
        {
            public decimal AvailableCredit { get; set; }
            public decimal OutstandingLoans { get; set; }
            public int CreditScore { get; set; }              // 300–850
            public string? CreditScoreText { get; set; }          // e.g., "Excellent"
            public decimal CreditUtilization { get; set; }        // 0–1 (e.g., 0.23 -> 23%)
            public string? PaymentHistoryStatus { get; set; }     // e.g., "On Time"
        }

        public class SectionsViewModel
        {
            public AccountSummaryViewModel? AccountSummary { get; set; }
            public CreditSummaryViewModel? CreditSummary { get; set; }

            public List<AccountViewModel> Accounts { get; set; } = new();
            public List<CardViewModel> Cards { get; set; } = new();
            public List<LoanViewModel> Loans { get; set; } = new();
        }
    }
}
