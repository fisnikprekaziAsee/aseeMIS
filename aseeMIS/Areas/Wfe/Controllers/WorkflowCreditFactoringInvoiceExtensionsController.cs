using dsWfe.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using CoreBankPartyService = dsBankParty.Core.Services.Interfaces.IBankPartyService<dsBankParty.Core.ViewModels.BankPartyViewModel>;

namespace aseeMIS.Areas.Wfe.Controllers;

[Area("Wfe")]
public class WorkflowCreditFactoringInvoiceExtensionsController(
    IWfeWorkflowCreditFactoringInvoiceExtensionService invoiceWorkflowService,
    CoreBankPartyService bankPartyService) : Controller
{
    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> GetStatuses(
        int bankPartyId,
        string? currentStatusCode = null,
        CancellationToken cancellationToken = default)
    {
        var bankParty = await bankPartyService.GetByIdAsync(bankPartyId);
        if (bankParty == null)
            return NotFound($"Bank Party with id {bankPartyId} was not found.");

        var workflowCode = invoiceWorkflowService.ResolveWorkflowCode(bankParty.IsIndividual);
        var statuses = await invoiceWorkflowService.GetAvailableStatusesAsync(
            bankParty.IsIndividual,
            currentStatusCode,
            cancellationToken);

        return Json(new
        {
            workflowCode,
            statuses = statuses.Select(x => new
            {
                CaseStatusCode = x.StatusCode,
                CaseStatusName = x.StatusName,
                x.CategoryStatusCode,
                x.Action,
                x.IsCurrent,
                x.SetEntityActive,
                x.RequiresDecisionReason,
                x.AllowedDecisionReasonCodes,
                x.IsEditable,
                x.AllowsStatusChange,
                x.IsTerminal,
                x.StepCode,
                x.SortOrder
            }),
            workflowSteps = statuses.Select(x => new
            {
                CaseStatusCode = x.StatusCode,
                x.CategoryStatusCode,
                x.StepCode,
                x.IsEditable,
                x.AllowsStatusChange,
                x.IsTerminal,
                x.SetEntityActive,
                x.SortOrder
            })
        });
    }
}
