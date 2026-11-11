using System.Security.Claims;
using dsMIS.Application.DTOs;
namespace aseeMIS.Utils;
public static class CrudActorFactory
{
    public static CrudActor FromPrincipal(ClaimsPrincipal principal) => new(
        principal.Identity?.IsAuthenticated == true,
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null,
        principal.FindFirstValue("AspNet.Identity.SecurityStamp"),
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList());
}
