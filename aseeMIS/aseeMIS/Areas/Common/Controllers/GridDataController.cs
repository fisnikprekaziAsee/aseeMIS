using dsMIS.Application.Utils;
using System.IO;
using System.Text;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Syncfusion.EJ2.Base;

namespace aseeMIS.Areas.Common.Controllers
{
    /// <summary>
    /// Backend for grids that run in RemoteMode (Syncfusion <c>ej.DataManager</c> with <c>UrlAdaptor</c>).
    /// Provides two endpoints that drive every paginated grid in the system:
    /// <list type="bullet">
    ///   <item><c>POST /api/griddata/paged</c> — calls <c>SelectPagedAsync</c> on any <c>IGenericCRUDService</c>; used by <c>CRUD_PAGED</c> grids.</item>
    ///   <item><c>POST /api/griddata/translator-paged</c> — calls <c>SelectForTranslatorPagedAsync</c> with source/destination languages from config; used by <c>TRANSLATOR_PAGED</c> grids.</item>
    /// </list>
    /// Auth flows through the same <see cref="AppAuthorizeHelper"/> as <c>CRUDController</c>, scoped by the
    /// <c>FormSessionId</c> request header (set by gridHelperGeneric.js when binding the DataManager).
    /// </summary>
    [Area("Common")]
    [AppAuthorizeAttribute]
    [Route("api/griddata")]
    public class GridDataController(
        IServiceProvider serviceProvider,
        IAppFormPermissionService formAccessService,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        IConfiguration configuration) : Controller
    {
        // Mirrors GridTranslatorWidget's fallback: keeps remote translator working when config is absent.
        private const string DefaultSourceLanguageCode = "sq-AL";
        private const string DefaultDestinationLanguageCode = "en-US";

        [HttpPost("paged")]
        public async Task<IActionResult> Paged(CancellationToken ct)
        {
            var dm = await ReadDataManagerRequestAsync(ct);
            return await HandleAsync(dm, methodName: "SelectPagedAsync", buildExtraArgs: null, ct);
        }

        [HttpPost("translator-paged")]
        public async Task<IActionResult> TranslatorPaged(CancellationToken ct)
        {
            var dm = await ReadDataManagerRequestAsync(ct);

            // Per-request override: TranslatorWidget sends the user-picked language pair via headers
            // (SourceLanguageCode / DestinationLanguageCode). When absent we fall back to configuration,
            // then to hard-coded defaults — same precedence as GridTranslatorWidget's first-page fetch.
            var src = HttpContext.Request.Headers["SourceLanguageCode"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(src)) src = configuration["Localization:SourceLanguageCode"];
            if (string.IsNullOrWhiteSpace(src)) src = DefaultSourceLanguageCode;

            var dst = HttpContext.Request.Headers["DestinationLanguageCode"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dst)) dst = configuration["Localization:DestinationLanguageCode"];
            if (string.IsNullOrWhiteSpace(dst)) dst = DefaultDestinationLanguageCode;

            // Translator services expect (source, destination, pageRequest, ct). PageRequest gets prepended below
            // so the final arg order matches SelectForTranslatorPagedAsync's signature.
            return await HandleAsync(
                dm,
                methodName: "SelectForTranslatorPagedAsync",
                buildExtraArgs: () => new object[] { src, dst },
                ct);
        }

        /// <summary>
        /// Syncfusion's <see cref="DataManagerRequest"/> ships with Newtonsoft <c>[JsonProperty]</c> attributes
        /// (camelCase: <c>skip</c>, <c>take</c>, <c>sorted</c>, ...). The app's default serializer is
        /// <c>System.Text.Json</c> with <c>PropertyNamingPolicy = null</c> which is case-sensitive and ignores
        /// those attributes — so <c>[FromBody] DataManagerRequest</c> would arrive with zero/empty fields and
        /// the grid would never paginate. We read the raw body and bind through Newtonsoft instead.
        /// </summary>
        private async Task<DataManagerRequest> ReadDataManagerRequestAsync(CancellationToken ct)
        {
            HttpContext.Request.EnableBuffering();
            HttpContext.Request.Body.Position = 0;

            using var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            var json = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return new DataManagerRequest();

            return JsonConvert.DeserializeObject<DataManagerRequest>(json) ?? new DataManagerRequest();
        }

        private async Task<IActionResult> HandleAsync(
            DataManagerRequest dm,
            string methodName,
            Func<object[]>? buildExtraArgs,
            CancellationToken ct)
        {
            if (dm is null)
                return BadRequest(new { error = "DataManagerRequest is required." });

            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext, formAccessService, menuService, userContextService,
                    formSessionId ?? string.Empty, EumFormAccessAction.READ);

            if (!isValid || appForm is null || formContext is null)
                return StatusCode(403, new { error = "No access." });

            var serviceInvoker = new ServiceInvoker(serviceProvider);
            var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModel);
            if (modelType is null)
                return StatusCode(500, new { error = "Unable to resolve grid model type." });

            var pageRequest = dm.ToPageRequest(modelType);

            var args = buildExtraArgs is null
                ? new object[] { pageRequest }
                : buildExtraArgs().Concat(new object[] { pageRequest }).ToArray();

            object? rawResult;
            try
            {
                rawResult = await serviceInvoker.InvokeAsync(projectNamespace, serviceInterface, modelType, methodName, args);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }

            // SelectPagedAsync / SelectForTranslatorPagedAsync return PagedResult<TViewModel>. Reflection avoids
            // having to cast to a specific generic argument here.
            if (rawResult is null)
                return Ok(new { result = Array.Empty<object>(), count = 0 });

            var resultType = rawResult.GetType();
            var itemsProp = resultType.GetProperty("Items");
            var countProp = resultType.GetProperty("TotalCount");

            var items = itemsProp?.GetValue(rawResult) ?? Array.Empty<object>();
            var count = countProp?.GetValue(rawResult) ?? 0;

            // Syncfusion UrlAdaptor expects { result, count } when RequiresCounts is true.
            return Ok(new { result = items, count });
        }
    }
}
