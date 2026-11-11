using dsBankParty.Core.Services.Interfaces;
using dsBankParty.Core.ViewModels;
using aseeMIS.Areas.BankParty.Enums;
using aseeMIS.Areas.BankParty.ViewModels;
using Microsoft.AspNetCore.Mvc;
using static aseeMIS.Areas.BankParty.ViewModels.BankClientSectionViewModel;

namespace aseeMIS.Areas.BankParty.Controllers
{
    [Area("BankParty")]
    public class BankDashboardController(IBankPartyService<BankPartyViewModel> service, IBankPartyIndividualService<BankPartyIndividualViewModel> bankPartyIndividualService) : Controller
    {
        #region Dashboard Models

        public class DashboardStats
        {
            public int TotalApplications { get; set; }
            public decimal ApplicationsChange { get; set; }
            public int ApprovedLoans { get; set; }
            public decimal ApprovedLoansChange { get; set; }
            public int ActiveClients { get; set; }
            public decimal ActiveClientsChange { get; set; }
            public string LoanPortfolio { get; set; } = "";
            public decimal LoanPortfolioChange { get; set; }
            public decimal DefaultRate { get; set; }
            public decimal DefaultRateChange { get; set; }
            public int NewCards { get; set; }
            public decimal NewCardsChange { get; set; }
        }

        public class DashboardViewModel
        {
            public DashboardStats Stats { get; set; } = new();
            public string? SearchQuery { get; set; }
            public bool HasSearched { get; set; }
            public List<BankClientWidgetEnum> Widgets { get; set; } = new();
        }

        #endregion

        #region Views

        public IActionResult Index(string? search)
        {
            var vm = new DashboardViewModel
            {
                Stats = new DashboardStats
                {
                    TotalApplications = 2847,
                    ApplicationsChange = 12.5m,
                    ApprovedLoans = 1923,
                    ApprovedLoansChange = 8.2m,
                    ActiveClients = 15432,
                    ActiveClientsChange = 15.3m,
                    LoanPortfolio = "$2.8M",
                    LoanPortfolioChange = 22.1m,
                    DefaultRate = 2.3m,
                    DefaultRateChange = -0.5m,
                    NewCards = 348,
                    NewCardsChange = 18.7m
                },
                SearchQuery = search,
                HasSearched = !string.IsNullOrWhiteSpace(search),
                Widgets =
                [
                    BankClientWidgetEnum.AccountActivity,
                    BankClientWidgetEnum.CreditInformation,
                    BankClientWidgetEnum.Accounts,
                    BankClientWidgetEnum.Cards,
                    BankClientWidgetEnum.Loans
                ]
            };

            return View("~/Areas/BankParty/Views/BankDashboard.cshtml", vm);
        }

        public IActionResult LoanClient(string? search)
        {
            var vm = new DashboardViewModel
            {
                SearchQuery = search,
                HasSearched = !string.IsNullOrWhiteSpace(search),

                Widgets =
                [
                    BankClientWidgetEnum.CreditInformation,
                    BankClientWidgetEnum.Loans
                ]
            };

            return View("~/Areas/BankParty/Views/BankDashboard.cshtml", vm);
        }

        #endregion

        #region Individual Header

        [HttpGet]
        public async Task<IActionResult> RenderIndividualHeader(string? idOrDisplay)
        {
            if (string.IsNullOrWhiteSpace(idOrDisplay))
                return Content(string.Empty);

            static string ExtractToken(string s)
            {
                var t = s.Trim();
                var i = t.LastIndexOf('(');
                var j = t.LastIndexOf(')');
                return (i >= 0 && j > i)
                    ? t.Substring(i + 1, j - i - 1).Trim()
                    : t.TrimStart('#');
            }

            var token = ExtractToken(idOrDisplay);

            BankPartyIndividualViewModel? headerModel;
            if (int.TryParse(token, out var bankPartyId))
            {
                BankPartyIndividualViewModel? bankPartyIndividualViewModel = await bankPartyIndividualService.GetByIdAsync(bankPartyId);
                headerModel = bankPartyIndividualViewModel;
            }
            else
            {
                headerModel = null;
            }

            if (headerModel is null)
                return Content(string.Empty);

            return ViewComponent(
                "BankEntityHeaderWidget",
                new
                {
                    viewPath = "~/Areas/BankParty/Views/_IndividualPartyHeader.cshtml",
                    model = headerModel
                });
        }

        #endregion

        #region Client Widgets (5 Separate Endpoints)

        [HttpGet]
        public IActionResult RenderAccountActivity(string? idOrDisplay)
        {
            if (!TryResolveClient(idOrDisplay))
                return Content(string.Empty);

            var model = new AccountSummaryViewModel
            {
                TotalBalance = 15420.50m + 45230.75m,
                MonthlyTurnover = 28750m,
                TransactionsThisMonth = 147,
                AverageTransaction = 308m,
                LargestTransaction = 8750m
            };

            return ViewComponent("AccountActivityWidget", model);
        }

        [HttpGet]
        public IActionResult RenderCreditInformation(string? idOrDisplay)
        {
            if (!TryResolveClient(idOrDisplay))
                return Content(string.Empty);

            var model = new CreditSummaryViewModel
            {
                AvailableCredit = 5000m - 1250.30m,
                OutstandingLoans = 32500m,
                CreditScore = 750,
                CreditScoreText = "Excellent",
                CreditUtilization = 0.23m,
                PaymentHistoryStatus = "On Time"
            };

            return ViewComponent("CreditInformationWidget", model);
        }

        [HttpGet]
        public IActionResult RenderAccounts(string? idOrDisplay)
        {
            if (!TryResolveClient(idOrDisplay))
                return Content(string.Empty);

            var model = new List<AccountViewModel>
            {
                new() { Name="Current Account", Number="1234567890", Balance=15420.50m, IsActive=true },
                new() { Name="Savings Account", Number="0987654321", Balance=45230.75m, IsActive=true }
            };

            return ViewComponent("AccountsWidget", model);
        }

        [HttpGet]
        public IActionResult RenderCards(string? idOrDisplay)
        {
            if (!TryResolveClient(idOrDisplay))
                return Content(string.Empty);

            var model = new List<CardViewModel>
            {
                new()
                {
                    Type="Visa",
                    Number="**** **** **** 1234",
                    Limit=5000m,
                    Balance=1250.30m,
                    Expires="12/25",
                    IsActive=true
                }
            };

            return ViewComponent("CardsWidget", model);
        }

        [HttpGet]
        public IActionResult RenderLoans(string? idOrDisplay)
        {
            if (!TryResolveClient(idOrDisplay, out int bankPartyId))
                return Content(string.Empty);

            return ViewComponent(
                "LoansWidget",
                new { bankPartyId = bankPartyId }
            );
        }
        #endregion

        #region Helpers

        private static bool TryResolveClient(string? idOrDisplay)
        {
            if (string.IsNullOrWhiteSpace(idOrDisplay))
                return false;

            var t = idOrDisplay.Trim();
            var i = t.LastIndexOf('(');
            var j = t.LastIndexOf(')');
            var id = (i >= 0 && j > i) ? t[(i + 1)..j].Trim() : t;

            return id.Equals("john", StringComparison.OrdinalIgnoreCase)
                || id.Equals("john smith", StringComparison.OrdinalIgnoreCase)
                || id.Equals("14", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        private static bool TryResolveClient(string? idOrDisplay, out int bankPartyId)
        {
            bankPartyId = 0;

            if (string.IsNullOrWhiteSpace(idOrDisplay))
                return false;

            var t = idOrDisplay.Trim();

            // handles "John Smith (123)" or "#123" or "123"
            var i = t.LastIndexOf('(');
            var j = t.LastIndexOf(')');

            var token = (i >= 0 && j > i)
                ? t.Substring(i + 1, j - i - 1).Trim()
                : t.TrimStart('#');

            return int.TryParse(token, out bankPartyId);
        }

    }
}
