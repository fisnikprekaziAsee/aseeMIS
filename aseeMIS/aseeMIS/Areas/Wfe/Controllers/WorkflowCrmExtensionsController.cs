using dsWfe.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Wfe.Controllers;

[Area("Wfe")]
public class WorkflowCrmExtensionsController(
    IWfeWorkflowCrmExtensionService workflowService) : Controller
{
    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> GetStatuses(
        bool isIndividual,
        bool? isResident = null,
        string? currentStatusCode = null,
        CancellationToken cancellationToken = default)
    {
        var entityCode = isIndividual
            ? "BANK_PARTY_INDIVIDUAL"
            : "BANK_PARTY_LEGAL";

        var statuses = await workflowService.GetAvailableStatusesAsync(
            entityCode,
            isIndividual,
            isResident: null,
            currentStatusCode: currentStatusCode,
            cancellationToken: cancellationToken);

        return Json(statuses);
    }
}
