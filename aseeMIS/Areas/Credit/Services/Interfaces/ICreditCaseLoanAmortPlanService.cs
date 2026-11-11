using dsCredit.Core.ViewModels;

namespace aseeMIS.Areas.Credit.Services.Interfaces
{
    public interface ICreditCaseLoanAmortPlanService
    {
        /// <summary>
        /// Regenerates amortization plan data on the loan view model.
        /// Requires <see cref="CreditCaseLoanViewModel.IsWorkflowStepEditable"/> to be set on the model.
        /// </summary>
        Task<CreditCaseLoanViewModel> RegenerateAsync(
            CreditCaseLoanViewModel model,
            string? generatedBy,
            CancellationToken cancellationToken = default);
    }
}
