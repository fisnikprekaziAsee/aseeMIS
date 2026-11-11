using dsMIS.Application.Utils;
using dsAppMng.Core.Services.Interfaces;
using dsMIS.Core.DTOs;
using dsMIS.Core.Enums;
using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Common.Controllers
{

    [Authorize]
    [Area("Common")]
    public class SDLTController(IStagingDataLogTableService stagingDataLogTableService, IServiceProvider serviceProvider, ToolbarItemsProvider toolbarItemsProvider,
                IAppFormMenuService menuService, IUserContextService userSecurityStampCacheService, ILocalizationService localizationService, IAppConstantService appConstantService,
                IAppFormPermissionService formAccessService, IAppSessionService dsSessionService) : Controller
    {

        public async Task<IActionResult> Index(int id)
        {
            // Step 1: Validate access permissions for this form and user

            var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userSecurityStampCacheService,
                    id,
                    EnumContextFormType.PARENT
                );

            if (!isValid || appForm == null || contextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");


            ViewBag.FormSessionId = contextSessionDto.FormSessionId;
            ViewBag.FormId = contextSessionDto.FormId;

            // Step 8: Dynamically generate toolbar and context menu based on permissions
            ViewBag.ToolbarItems = toolbarItemsProvider.GetToolbarItems(appForm, contextSessionDto, EnumFormType.SDLT);
            ViewBag.ContextMenuItems = await toolbarItemsProvider.GetContextMenuItems(contextSessionDto);


            return View("~/Areas/Common/Views/SDLT.cshtml");

        }

        [HttpGet]
        public async Task<IActionResult> GetData(string startDate, string endDate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
                {
                    return BadRequest(new { success = false, message = "Start and End dates are required." });
                }

                if (!DateTime.TryParse(startDate, out DateTime parsedStartDate) ||
                    !DateTime.TryParse(endDate, out DateTime parsedEndDate))
                {
                    return BadRequest(new { success = false, message = "Invalid date format." });
                }

                // 🔹 Get FormSessionId from request headers
                var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
                if (string.IsNullOrEmpty(formSessionId))
                {
                    return Unauthorized(new { success = false, message = "Missing FormSessionId." });
                }

                // 🔹 Validate access using your AppAuthorizeHelper
                var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) =
                    await AppAuthorizeHelper.ValidateActionAsync(
                        HttpContext,
                        formAccessService,
                        menuService,
                        userSecurityStampCacheService,
                        formSessionId,
                        EumFormAccessAction.READ
                    );

                if (!isValid || appForm == null || contextSessionDto == null)
                {
                    return Unauthorized(new { success = false, message = "Invalid or missing form context." });
                }

                // 🔹 Use FormId from context, NOT UserId
                var data = await stagingDataLogTableService.SelectAllByIntervalAsync(
                    contextSessionDto.FormId,
                    contextSessionDto.LanguageCode,
                    parsedStartDate,
                    parsedEndDate
                );

                if (data == null || !data.Any())
                {
                    var message = await localizationService
                        .FindServerSideErrorMessageByKeyAndLanguageCodeAsync("NotFound", contextSessionDto.LanguageCode);

                    return Json(new { success = false, message });
                }

                return Json(data);
            }
            catch (AppException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Unexpected server error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SDLTReviewForm(string targetId, int? formId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetId))
                    return BadRequest(new { success = false, message = "TargetId is required." });

                var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
                var isReviewAll = HttpContext.Request.Headers["IsReviewAll"].FirstOrDefault();

                var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) = await AppAuthorizeHelper.ValidateActionAsync(HttpContext, formAccessService,
                                                                                                            menuService, userSecurityStampCacheService, formSessionId, EumFormAccessAction.APPROVE);

                if (appForm.FormType == "360")
                {
                    contextSessionDto.FormId = (int)appForm.FlowForm360Id;
                }

                if (!isValid || appForm == null || contextSessionDto == null)
                    return Unauthorized(new { success = false, message = "Invalid or missing form context" });

                // Load constants
                ViewBag.ConstantValues = await appConstantService
                    .SelectForDropdownAsync("SDLRejectType", contextSessionDto.LanguageCode);
                ViewBag.FormSessionId = formSessionId;
                ViewBag.IsReviewAll = isReviewAll;



                int contextSessionDtoFormId = formId == null ? contextSessionDto.FormId : (int)formId;
                ViewBag.FormId = contextSessionDtoFormId;
                var stagingDataLogTable = await stagingDataLogTableService.GetStagingDataLogAsync(
                    contextSessionDtoFormId,
                    targetId,
                    contextSessionDto.LanguageCode
                );

                if (stagingDataLogTable == null)
                    return NotFound(new { success = false, message = "No data found for this target." });

                return PartialView("~/Areas/Common/Views/_SDLTReviewForm.cshtml", stagingDataLogTable);
            }
            catch (AppException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Unexpected error occurred." });
            }
        }


        [HttpPost]
        public async Task<IActionResult> Review(StagingDataLogTableDto stagingDataLogTableDto)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            var isReviewAll = HttpContext.Request.Headers["IsReviewAll"].FirstOrDefault();

            var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) = await AppAuthorizeHelper.ValidateActionAsync(HttpContext, formAccessService,
                                                                                                            menuService, userSecurityStampCacheService, formSessionId, EumFormAccessAction.APPROVE);

            if (!isValid || appForm == null || contextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");

            stagingDataLogTableDto.ReviewedBy = contextSessionDto.UserId;

            var tempAppForm = menuService.FindByFormIdAsync(int.Parse(stagingDataLogTableDto.FormId)).Result;
            viewModel = $"ViewModels.{menuService.FindByFormIdAsync(int.Parse(stagingDataLogTableDto.FormId)).Result.ViewModelName}ViewModel";
            projectNamespace = $"ds{tempAppForm.ProjectNameAcronym}.Core";
            serviceInterface = $"Services.Interfaces.I{tempAppForm.ModelName}Service";
            viewModel = $"ViewModels.{tempAppForm.ViewModelName}ViewModel";

            try
            {
                if (isReviewAll == "false" || isReviewAll == "")
                {
                    var invoker = new ServiceInvoker(serviceProvider);
                    var modelType = invoker.GetViewModelType(projectNamespace, viewModel);
                    await invoker.InvokeAsync(projectNamespace, serviceInterface, modelType, "ReviewAsync", stagingDataLogTableDto, contextSessionDto.LanguageCode);

                    bool isDelete = stagingDataLogTableDto.OperationTypeCode.Equals("D");
                    return Ok(new
                    {
                        success = true,
                        message = localizationService.FindServerSideSuccessMessageByKeyAndLanguageCodeAsync("REVIEW", contextSessionDto.LanguageCode).Result + stagingDataLogTableDto.TargetId,
                        isApproved = stagingDataLogTableDto.IsApproved,
                        isDeleted = isDelete
                    });
                }
                else
                {
                    await stagingDataLogTableService.ReviewAllAsync((int)appForm.FlowForm360Id, stagingDataLogTableDto.TargetId, contextSessionDto.LanguageCode, contextSessionDto.UserId, stagingDataLogTableDto.IsApproved);

                    return Ok(new
                    {
                        success = true,
                        message = localizationService.FindServerSideSuccessMessageByKeyAndLanguageCodeAsync("REVIEW", contextSessionDto.LanguageCode).Result + stagingDataLogTableDto.TargetId,
                        isApproved = stagingDataLogTableDto.IsApproved,
                    });
                }
            }
            catch (AppException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }
    }
}
