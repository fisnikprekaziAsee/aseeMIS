using dsBankParty.Core.Services.Interfaces;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.BankParty.Controllers;

[Area("BankParty")]
[AppAuthorize]
public class BankPartySignicatWebhookLogsController(
    IBankPartySignicatWebhookLogService service,
    IAppFormMenuService menuService,
    IUserContextService userContextService,
    IAppFormPermissionService formAccessService) : Controller
{
    [HttpPost]
    [Produces("application/json")]
    public async Task<IActionResult> Consolidate(
        [FromBody] ConsolidateRequest request,
        CancellationToken cancellationToken)
    {
        var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
        var (isValid, appForm, formContext, _, _, _) =
            await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userContextService,
                formSessionId ?? string.Empty,
                EumFormAccessAction.READ);

        if (!isValid || appForm == null || formContext == null)
            return Ok(new { success = false, message = "No access to consolidate this webhook log." });

        if (request?.Id is null or <= 0)
            return Ok(new { success = false, message = "Webhook log Id is required." });

        try
        {
            var message = await service.ConsolidateAsync(request.Id.Value, cancellationToken);
            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    public sealed class ConsolidateRequest
    {
        public int? Id { get; set; }
    }
}
