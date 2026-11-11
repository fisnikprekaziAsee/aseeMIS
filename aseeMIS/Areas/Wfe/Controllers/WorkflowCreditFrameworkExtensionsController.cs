using dsWfe.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using CoreBankPartyService = dsBankParty.Core.Services.Interfaces.IBankPartyService<dsBankParty.Core.ViewModels.BankPartyViewModel>;

namespace aseeMIS.Areas.Wfe.Controllers;

[Area("Wfe")]
public class WorkflowCreditFrameworkExtensionsController(
    IWfeWorkflowCreditFrameworkExtensionService workflowService,
    IWfeDecisionReasonService decisionReasonService,
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

        var statuses = await workflowService.GetAvailableStatusesAsync(
            bankParty.IsIndividual,
            currentStatusCode,
            cancellationToken);

        return Json(statuses.Select(x => new
        {
            FrameworkStatusCode = x.StatusCode,
            FrameworkStatusName = x.StatusName,
            x.Action,
            x.IsCurrent,
            x.SetEntityActive,
            x.RequiresDecisionReason,
            x.AllowedDecisionReasonCodes
        }));
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> GetDecisionReasons(
        CancellationToken cancellationToken = default)
    {
        var decisionReasons =
            await decisionReasonService.SelectAllActiveForDropdownAsync(cancellationToken);

        return Json(decisionReasons.Select(x => new
        {
            FrameworkDecisionReasonCode = x.DecisionReasonCode,
            FrameworkDecisionReasonName = x.DecisionReasonName
        }));
    }
}
