using dsMIS.Application.Services.Interfaces;
using dsMIS.Application.DTOs;
using dsMIS.Application.Utils;
using dsMIS.Core.Attributes;
using dsMIS.Core.Enums;
using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using dsMIS.Core.Utils;
using dsCredit.Core.Services.Interfaces;
using aseeMIS.Areas.Common.DTOs;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
using System.Reflection;

namespace aseeMIS.Areas.Common.Controllers
{
    [Area("Common")]
    [AppAuthorizeAttribute]
    public class CRUDController(
        IServiceProvider serviceProvider,
        ToolbarItemsProvider toolbarItemsProvider,
        ILocalizationService localizationService,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        IAppFormPermissionService formAccessService,
        IConfiguration config,
        ISecureFileStorage secureFileStorage,
        IGenericCrudOrchestrator crudOrchestrator) : Controller
    {
        public async Task<IActionResult> Index(int id)
        {
            var appForm = await menuService.FindByFormIdAsync(id);
            if (appForm == null)
                return NotFound($"Form {id} is not available in the active menu configuration. Check AppForms and publish the menu or restart the application after updating its configuration.");

            // FormType in DB is a string; map to the enum so the view can pick the right ViewComponent.
            // PAGED variants opt-in to server-side paging (Syncfusion DataManager + UrlAdaptor) — see GridDataController.
            // SDLT = read-only/log toolbar (no Add/Edit/Delete, only Approve/Search/Export).
            var formType = EnumFormTypeMapper.MapFromAppForm(appForm.FormType);

            // Translator grids share a stable grid name so the TranslatorWidget panel (search/save bar) can target them.
            var isAnyTranslator = formType is EnumFormType.TRANSLATOR or EnumFormType.TRANSLATOR_PAGED;

            return View("~/Areas/Common/Views/CRUD.cshtml",
                new GridRequest
                {
                    FormId = id,
                    ContextFormType = EnumContextFormType.PARENT,
                    GridName = isAnyTranslator ? "TranslatorGrid" : "",
                    FormType = formType
                });
        }

        [HttpPost]
        public async Task<IActionResult> RefreshGridData([FromBody] GridRefreshRequest request)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    formSessionId,
                    requiredPermission: EumFormAccessAction.READ);

            if (!isValid || appForm == null || formContext == null)
                return Forbid();

            var serviceInvoker = new ServiceInvoker(serviceProvider);
            var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModel);
            if (modelType == null)
                return BadRequest(new { success = false, message = "The grid model type could not be resolved." });

            string methodName;
            var args = new List<object>();

            if (request.ContextFormType == EnumContextFormType.CHILD)
            {
                methodName = string.IsNullOrWhiteSpace(appForm.FlowMethodName)
                    ? "SelectAllDetailAsync"
                    : appForm.FlowMethodName;

                if (!string.IsNullOrWhiteSpace(request.ForeignAttributeName))
                    args.Add(request.ForeignAttributeName);
                if (!string.IsNullOrWhiteSpace(request.TargetId))
                    args.Add(request.TargetId);
            }
            else
            {
                methodName = !string.IsNullOrWhiteSpace(appForm.FlowMethodName)
                    ? appForm.FlowMethodName
                    : appForm.TokenClaimType == true
                        ? "SelectAllByClaimTypeAsync"
                        : "SelectAllAsync";

                if (appForm.TokenUserId == true)
                    args.Add(formContext.UserId);
                if (appForm.TokenLanguageCode == true)
                    args.Add(formContext.LanguageCode);
                if (appForm.TokenClaimType == true)
                {
                    var claims = string.Join(";", HttpContext.User.Claims
                        .Where(c => c.Type == "DS")
                        .Select(c => c.Value));
                    args.Add(claims);
                }
            }

            try
            {
                var data = await serviceInvoker.InvokeAsync(
                    projectNamespace,
                    serviceInterface,
                    modelType,
                    methodName,
                    args.ToArray());

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        private async Task<IActionResult> ExecuteCrudOperationAsync(object request, EumFormAccessAction operationType)
        {
            var result = await crudOrchestrator.ExecuteAsync(
                CrudActorFactory.FromPrincipal(HttpContext.User),
                HttpContext.Request.Headers["FormSessionId"].FirstOrDefault(),
                request?.ToString(), operationType,
                new MvcCrudModelValidator(ModelState, model => TryValidateModel(model)),
                async (model, modelType) =>
                {
                    foreach (var up in FindUploadProps(modelType))
                        await HandleUploadAsync(model, modelType, up.Prop, up.Attr);
                });

            // Preserve legacy MVC envelopes, including its historical success:true denial flag.
            if (result.Errors != null)
                return Ok(new { success = false, message = result.Message, errors = result.Errors });
            if (result.HasUpdatedRow)
                return Ok(new { success = result.Outcome == CrudOutcome.Succeeded, message = result.Message, updatedRow = result.UpdatedRow });
            return Ok(new { success = result.Outcome is CrudOutcome.Succeeded or CrudOutcome.Denied, message = result.Message });
        }

        [HttpGet]
        public async Task<IActionResult> Download(string path, string? fileName = null, string? formSessionId = null)
        {
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    formSessionId,
                    EumFormAccessAction.EXPORT);

            if (!isValid || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            if (string.IsNullOrWhiteSpace(path))
                return BadRequest("File path is required.");

            byte[]? bytes;
            try
            {
                bytes = await secureFileStorage.ReadAsync(path, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException)
            {
                return BadRequest("Invalid file path.");
            }

            if (bytes == null)
                return NotFound("File not found.");

            var extension = Path.GetExtension(path);

            if (extension.Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                bytes = DecryptStoredFile(bytes);
            }

            // ?? Resolve content type AFTER decrypt
            var contentType = extension.Equals(".bin", StringComparison.OrdinalIgnoreCase)
                ? ResolveContentTypeFromBytes(bytes) // ?? NEW
                : ResolveContentTypeFromExtension(extension);

            var originalName = !string.IsNullOrWhiteSpace(fileName)
                ? fileName
                : Path.GetFileNameWithoutExtension(path);

            if (extension.Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                var correctExt = GetExtensionFromContentType(contentType);
                originalName = Path.ChangeExtension(originalName, correctExt);
            }
            else
            {
                originalName = Path.GetFileName(path);
            }

            var downloadFileName = originalName;

            return File(bytes, contentType, downloadFileName);
        }

        [HttpGet]
        public async Task<IActionResult> Preview(string path, string? formSessionId = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest("File path is required.");

            // Auth: when a formSessionId is supplied (grid cells always pass it now), require
            // READ permission on the originating form. This brings Preview to parity with
            // grid-row visibility — a user who cannot READ the form cannot peek at files via
            // path-guessing either. Calls without formSessionId remain permissive (legacy
            // callers / embedded partials) but still rely on path-traversal protection below.
            if (!string.IsNullOrWhiteSpace(formSessionId))
            {
                var (isValid, _, _, _, _, _) =
                    await AppAuthorizeHelper.ValidateActionAsync(
                        HttpContext,
                        formAccessService,
                        menuService,
                        userContextService,
                        formSessionId,
                        EumFormAccessAction.READ);

                if (!isValid)
                    return RedirectToAction("NoAccess", "Error");
            }

            byte[]? bytes;
            try
            {
                bytes = await secureFileStorage.ReadAsync(path, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException)
            {
                return BadRequest("Invalid file path.");
            }

            if (bytes == null)
                return NotFound("File not found.");

            var extension = Path.GetExtension(path);

            if (extension.Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                bytes = DecryptStoredFile(bytes);
            }

            // ?? Resolve content type
            var contentType = extension.Equals(".bin", StringComparison.OrdinalIgnoreCase)
                ? ResolveContentTypeFromBytes(bytes)
                : ResolveContentTypeFromExtension(extension);

            // ? FORCE INLINE (preview instead of download)
            Response.Headers["Content-Disposition"] = $"inline; filename=\"preview\"";

            return File(bytes, contentType);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] object request) =>
            await ExecuteCrudOperationAsync(request, EumFormAccessAction.ADD);

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] object request) =>
            await ExecuteCrudOperationAsync(request, EumFormAccessAction.UPDATE);

        [HttpPost]
        public async Task<IActionResult> BatchUpdateAdditionalForm([FromBody] AdditionalFormBatchSaveRequest request)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, parentAppForm, formContext, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    formSessionId,
                    EumFormAccessAction.ADD);

            if (!isValid || parentAppForm == null || formContext == null)
                return Ok(new { success = false, message = "No access." });

            if (request?.Fields == null || request.Fields.Count == 0)
                return Ok(new { success = false, message = "No fields to save." });

            if (request.FormId <= 0)
                return Ok(new { success = false, message = "Additional FormId is required." });

            var appForm = await menuService.FindByFormIdAsync(request.FormId);
            if (appForm == null || appForm.IsActive != true)
                return Ok(new { success = false, message = "Additional form is not configured." });

            var projectNamespace = $"ds{appForm.ProjectNameAcronym}.Core";
            var serviceInterface = $"Services.Interfaces.I{appForm.ModelName}Service";
            var viewModel = $"ViewModels.{appForm.ViewModelName}ViewModel";

            var serviceInvoker = new ServiceInvoker(serviceProvider);
            var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModel);

            var foreignKeyProp = modelType.GetProperty(
                request.ForeignKeyPropertyName ?? appForm.FlowForeignKeyAttributeName ?? string.Empty,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (foreignKeyProp == null)
            {
                foreignKeyProp =
                    modelType.GetProperty("BankPartyId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p =>
                            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(p.Name, "FieldId", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(p.Name, "AdditionalFieldId", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(p.Name, "ValueId", StringComparison.OrdinalIgnoreCase));
            }

            if (foreignKeyProp == null)
                return Ok(new { success = false, message = $"Foreign key '{appForm.FlowForeignKeyAttributeName}' was not found on {modelType.Name}." });

            var fieldIdProp = modelType.GetProperty("FieldId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                              ?? modelType.GetProperty("AdditionalFieldId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            var valueIdProp = modelType.GetProperty("ValueId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var valueTextProp = modelType.GetProperty("ValueText", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var isActiveProp = modelType.GetProperty("IsActive", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (fieldIdProp == null || valueIdProp == null || valueTextProp == null)
                return Ok(new { success = false, message = $"The ViewModel '{modelType.Name}' does not expose FieldId/ValueId/ValueText as expected." });

            var typedRows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(modelType))!;
            foreach (var field in request.Fields)
            {
                var row = Activator.CreateInstance(modelType)
                    ?? throw new InvalidOperationException($"Could not create instance of {modelType.Name}.");

                foreignKeyProp.SetValue(row, ConvertToPropertyType(request.TargetId, foreignKeyProp.PropertyType));
                fieldIdProp.SetValue(row, ConvertToPropertyType(field.AdditionalFieldId, fieldIdProp.PropertyType));
                valueIdProp.SetValue(row, ConvertToPropertyType(field.ValueId, valueIdProp.PropertyType));
                valueTextProp.SetValue(row, field.ValueText);

                if (isActiveProp != null)
                    isActiveProp.SetValue(row, !string.IsNullOrWhiteSpace(field.ValueText));

                typedRows.Add(row);
            }

            try
            {
                await serviceInvoker.InvokeAsync(
                    projectNamespace,
                    serviceInterface,
                    modelType,
                    "BatchUpsertAsync",
                    typedRows,
                    formContext);

                return Ok(new { success = true, message = "Additional fields saved successfully." });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Remove([FromBody] object request) =>
            await ExecuteCrudOperationAsync(request, EumFormAccessAction.REMOVE);

        [HttpPost]
        public async Task<IActionResult> Publish()
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    formSessionId,
                    EumFormAccessAction.PUBLISH);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            var serviceInvoker = new ServiceInvoker(serviceProvider);
            var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModel);

            try
            {
                await serviceInvoker.InvokeAsync(
                    projectNamespace,
                    serviceInterface,
                    modelType,
                    "PublishAsync",
                    formContext);

                return Ok(new
                {
                    success = true,
                    message = localizationService
                        .FindServerSideSuccessMessageByKeyAndLanguageCodeAsync("PUBLISH", formContext.LanguageCode)
                        .Result
                });
            }
            catch (AppException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        #region Private Helpers

        private static List<(PropertyInfo Prop, FileUploadAttribute Attr)> FindUploadProps(Type modelType)
        {
            return modelType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new
                {
                    Prop = p,
                    Attr = p.GetCustomAttribute<FileUploadAttribute>()
                })
                .Where(x => x.Attr != null)
                .Select(x => (x.Prop, x.Attr!))
                .ToList();
        }

        private static string GetTransportFieldName(PropertyInfo uploadProp, FileUploadAttribute attr)
        {
            return !string.IsNullOrWhiteSpace(attr.UploadTransportFieldName)
                ? attr.UploadTransportFieldName
                : uploadProp.Name + "Base64";
        }

        private static string GetOriginalFileNameFieldName(PropertyInfo uploadProp, FileUploadAttribute attr)
        {
            return !string.IsNullOrWhiteSpace(attr.OriginalFileNameField)
                ? attr.OriginalFileNameField
                : uploadProp.Name + "OriginalFileName";
        }

        private static string GetContentTypeFieldName(PropertyInfo uploadProp, FileUploadAttribute attr)
        {
            return !string.IsNullOrWhiteSpace(attr.ContentTypeField)
                ? attr.ContentTypeField
                : uploadProp.Name + "ContentType";
        }

        private byte[] DecryptStoredFile(byte[] bytes)
        {
            if (IsKnownFile(bytes))
                return bytes;

            foreach (var key in GetFileSecurityKeys())
            {
                if (FileCryptoHelper.TryDecrypt(bytes, key, out var plain) && IsKnownFile(plain))
                    return plain;
            }

            return bytes;
        }

        private static bool IsKnownFile(byte[] fileBytes)
        {
            if (fileBytes is not { Length: >= 4 })
                return false;

            return (fileBytes[0] == 0xFF && fileBytes[1] == 0xD8)
                || (fileBytes[0] == 0x89 && fileBytes[1] == 0x50)
                || (fileBytes[0] == 0x25 && fileBytes[1] == 0x50)
                || (fileBytes[0] == 0x47 && fileBytes[1] == 0x49)
                || (fileBytes.Length >= 12
                    && fileBytes[0] == 0x52 && fileBytes[1] == 0x49
                    && fileBytes[8] == 0x57 && fileBytes[9] == 0x45);
        }

        private IEnumerable<string> GetFileSecurityKeys()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var activeVersion = config["FileSecurity:ActiveKeyVersion"];
            var activeKey = config[$"FileSecurity:Keys:{activeVersion}"];
            if (!string.IsNullOrWhiteSpace(activeKey) && seen.Add(activeKey))
                yield return activeKey;

            foreach (var child in config.GetSection("FileSecurity:Keys").GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Value) && seen.Add(child.Value))
                    yield return child.Value;
            }
        }

        private string ResolveContentTypeFromBytes(byte[] fileBytes)
        {
            if (fileBytes.Length < 4)
                return "application/octet-stream";

            // JPEG
            if (fileBytes[0] == 0xFF && fileBytes[1] == 0xD8)
                return "image/jpeg";

            // PNG
            if (fileBytes[0] == 0x89 && fileBytes[1] == 0x50)
                return "image/png";

            // PDF
            if (fileBytes[0] == 0x25 && fileBytes[1] == 0x50)
                return "application/pdf";

            return "application/octet-stream";
        }

        private string GetExtensionFromContentType(string contentType)
        {
            return contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "application/pdf" => ".pdf",
                _ => ".bin"
            };
        }

        private static bool TryExtractBytesFromTransportField(
            object model,
            Type modelType,
            PropertyInfo uploadProp,
            FileUploadAttribute attr,
            out byte[] bytes,
            out string? originalFileName,
            out string? contentType)
        {
            bytes = Array.Empty<byte>();
            originalFileName = null;
            contentType = null;

            var transportFieldName = GetTransportFieldName(uploadProp, attr);
            var transportProp = modelType.GetProperty(transportFieldName, BindingFlags.Public | BindingFlags.Instance);

            if (transportProp == null || transportProp.PropertyType != typeof(string))
                return false;

            var value = (transportProp.GetValue(model) as string)?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return false;

            var commaIndex = value.IndexOf(',');
            if (commaIndex < 0)
                return false;

            var metadata = value[..commaIndex];
            var base64 = value[(commaIndex + 1)..].Trim();

            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            var semicolonIndex = metadata.IndexOf(';');
            if (semicolonIndex > 5)
            {
                contentType = metadata.Substring(5, semicolonIndex - 5);
            }

            var originalFileNameProp = modelType.GetProperty(GetOriginalFileNameFieldName(uploadProp, attr), BindingFlags.Public | BindingFlags.Instance);
            if (originalFileNameProp?.PropertyType == typeof(string))
            {
                originalFileName = originalFileNameProp.GetValue(model) as string;
            }

            var contentTypeProp = modelType.GetProperty(GetContentTypeFieldName(uploadProp, attr), BindingFlags.Public | BindingFlags.Instance);
            if (string.IsNullOrWhiteSpace(contentType) && contentTypeProp?.PropertyType == typeof(string))
            {
                contentType = contentTypeProp.GetValue(model) as string;
            }

            return bytes.Length > 0;
        }

        private static string BuildAllowedSubfolder(
            FileUploadAttribute attr,
            string id,
            object model,
            Type modelType)
        {
            var categoryFolder = attr.CategoryFolder;
            if (!string.IsNullOrWhiteSpace(attr.CategoryFolderBooleanProperty))
            {
                var categoryProperty = modelType.GetProperty(
                    attr.CategoryFolderBooleanProperty,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (categoryProperty?.PropertyType == typeof(bool))
                {
                    var categoryFlag = (bool)(categoryProperty.GetValue(model) ?? false);
                    categoryFolder = categoryFlag
                        ? attr.TrueCategoryFolder
                        : attr.FalseCategoryFolder;
                }
            }

            var uploadFolder = attr.SubfolderStrategy == SubfolderStrategy.ByPartyBucket
                ? (attr.UploadFolder?.Trim() ?? string.Empty)
                : (string.IsNullOrWhiteSpace(attr.UploadFolder) ? "Uploads" : attr.UploadFolder);

            return attr.SubfolderStrategy switch
            {
                SubfolderStrategy.ByYearMonth => Path.Combine(
                    uploadFolder,
                    DateTime.UtcNow.Year.ToString(),
                    DateTime.UtcNow.Month.ToString("D2")),

                SubfolderStrategy.ByYear => Path.Combine(
                    uploadFolder,
                    DateTime.UtcNow.Year.ToString()),

                SubfolderStrategy.ByPrimaryKey => Path.Combine(uploadFolder, id),

                SubfolderStrategy.ByPartyBucket => BuildPartyBucketPath(uploadFolder, attr, id, categoryFolder),

                _ => uploadFolder
            };
        }

        /// <summary>
        /// Builds {uploadFolder?}/{CategoryFolder?}/B_{bucket:D6}/{id}/{PartySubfolder?}
        /// for ByPartyBucket.
        /// All of a client's files (images, documents, consents...) end up in ONE folder,
        /// while each bucket directory stays well under filesystem hotspot thresholds.
        /// </summary>
        private static string BuildPartyBucketPath(
            string uploadFolder,
            FileUploadAttribute attr,
            string id,
            string? categoryFolder)
        {
            // Non-numeric ids (e.g. GUID fallback) bypass bucketing — degrade gracefully
            // by storing under the category folder (or the upload folder) directly.
            if (!int.TryParse(id, out var numericId) || numericId <= 0)
            {
                var nonNumericPath = string.IsNullOrWhiteSpace(categoryFolder)
                    ? Path.Combine(uploadFolder, id)
                    : Path.Combine(uploadFolder, categoryFolder, id);

                return string.IsNullOrWhiteSpace(attr.PartySubfolder)
                    ? nonNumericPath
                    : Path.Combine(nonNumericPath, attr.PartySubfolder!);
            }

            var bucketSize = attr.BucketSize > 0 ? attr.BucketSize : 1000;

            // Bucket #1 holds ids 1..bucketSize, #2 holds (bucketSize+1)..2*bucketSize, ...
            // Bucket name is padded (B_000001) so admins browsing folders see them in
            // natural order; this has zero impact on filesystem lookup performance on
            // NTFS/ext4/xfs/btrfs (all index entries via B-tree / HTree), it's purely
            // a sort-order convenience.
            var bucket = ((numericId - 1) / bucketSize) + 1;
            var bucketFolder = $"B_{bucket:D6}";       // up to 100,000 buckets ˜ 100M clients at default BucketSize
            var idFolder = numericId.ToString();       // raw id — padding gives no FS lookup benefit on modern filesystems

            var partyPath = string.IsNullOrWhiteSpace(categoryFolder)
                ? Path.Combine(uploadFolder, bucketFolder, idFolder)
                : Path.Combine(uploadFolder, categoryFolder, bucketFolder, idFolder);

            return string.IsNullOrWhiteSpace(attr.PartySubfolder)
                ? partyPath
                : Path.Combine(partyPath, attr.PartySubfolder!);
        }

        private static string ResolveExtension(string? originalFileName, string? contentType, FileType fileType)
        {
            if (!string.IsNullOrWhiteSpace(originalFileName))
            {
                var extFromName = Path.GetExtension(originalFileName);
                if (!string.IsNullOrWhiteSpace(extFromName))
                    return extFromName;
            }

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                switch (contentType.Trim().ToLowerInvariant())
                {
                    case "image/jpeg":
                    case "image/jpg":
                        return ".jpg";
                    case "image/png":
                        return ".png";
                    case "image/gif":
                        return ".gif";
                    case "image/webp":
                        return ".webp";
                    case "application/pdf":
                        return ".pdf";
                    case "application/msword":
                        return ".doc";
                    case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
                        return ".docx";
                    case "application/vnd.ms-excel":
                        return ".xls";
                    case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
                        return ".xlsx";
                }
            }

            return fileType == FileType.Image ? ".png" : ".bin";
        }

        private static string ResolveContentTypeFromExtension(string? extension)
        {
            var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();

            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }

        private async Task HandleUploadAsync(
            object model,
            Type modelType,
            PropertyInfo uploadProp,
            FileUploadAttribute attr)
        {
            if (!TryExtractBytesFromTransportField(
                    model,
                    modelType,
                    uploadProp,
                    attr,
                    out var bytes,
                    out var originalFileName,
                    out var contentType))
            {
                return;
            }

            if (attr.MaxFileSize > 0 && bytes.Length > attr.MaxFileSize)
                throw new InvalidOperationException($"File exceeds max size {(attr.MaxFileSize / (1024 * 1024))}MB.");

            if (uploadProp.PropertyType != typeof(string))
                throw new InvalidOperationException($"Property '{uploadProp.Name}' must be string when saving to path.");

            await EnsureFactoringInvoiceStorageContextAsync(model, modelType);

            var id = ResolveUploadSubfolderId(model, modelType, attr);
            if (string.Equals(attr.PartySubfolder, "F", StringComparison.OrdinalIgnoreCase)
                && (!int.TryParse(id, out var bucketPartyId) || bucketPartyId <= 0))
            {
                throw new InvalidOperationException("Credit case nuk ka BankPartyId të vlefshëm për ngarkimin e faturës.");
            }

            // ?? STEP 1: Resolve key
            var keyVersion = attr.EncryptionKeyVersion
                ?? config["FileSecurity:ActiveKeyVersion"];

            var key = config[$"FileSecurity:Keys:{keyVersion}"];

            if (attr.EncryptFile && string.IsNullOrEmpty(key))
                throw new Exception($"Encryption key not found for version: {keyVersion}");

            // ?? STEP 2: Resolve extension FIRST (before encryption override)
            var extension = ResolveExtension(originalFileName, contentType, attr.FileType);

            // ?? STEP 3: Override extension if encrypted
            if (attr.EncryptFile)
            {
                extension = attr.EncryptedExtension; // usually ".bin"
            }

            var relativeFolder = BuildAllowedSubfolder(attr, id, model, modelType);

            string fileName;

            if (string.Equals(attr.PartySubfolder, "F", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ResolveFactoringInvoiceFileNamePrefix(model, modelType);
                var invoiceNumber = modelType.GetProperty(
                    "InvoiceNumber",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?.GetValue(model)?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(invoiceNumber))
                    throw new InvalidOperationException("Invoice number is required before uploading the invoice file.");

                fileName = $"{SanitizeFileName(prefix)}_{SanitizeFileName(invoiceNumber)}{extension}";
            }
            else
            {
                string? suffix = string.IsNullOrWhiteSpace(attr.FileNameSuffix)
                    ? null
                    : "_" + SanitizeFileName(attr.FileNameSuffix);

                if (attr.UseOriginalFileName && !string.IsNullOrWhiteSpace(originalFileName))
                {
                    var baseName = Path.GetFileNameWithoutExtension(originalFileName);
                    var sanitized = SanitizeFileName(baseName);

                    if (string.IsNullOrWhiteSpace(sanitized))
                    {
                        fileName = $"{id}{suffix}{extension}";
                    }
                    else
                    {
                        fileName = $"{sanitized}{suffix}{extension}";
                    }
                }
                else if (attr.OverwriteExisting || attr.FileType == FileType.Image)
                {
                    // STABLE filename: when OverwriteExisting is on, the DB path stays the
                    // same across uploads so the same logical "slot" is reused (and the
                    // previous file is archived below). Images always used stable names.
                    fileName = $"{id}{suffix}{extension}";
                }
                else
                {
                    // UNIQUE filename: when OverwriteExisting is off we keep historical
                    // versions alive by giving every upload its own filename.
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    var uniq = Guid.NewGuid().ToString("N")[..8];

                    fileName = $"{id}{suffix}_{timestamp}_{uniq}{extension}";
                }
            }

            var relativePath = Path.Combine(relativeFolder, fileName).Replace('\\', '/');

            // ?? STEP 4: Encrypt BEFORE saving
            byte[] finalBytes = bytes;

            if (attr.EncryptFile)
            {
                finalBytes = FileCryptoHelper.Encrypt(bytes, key);
            }

            // ?? STEP 4b: Archive-on-overwrite
            // When the slot is being reused (OverwriteExisting=true) we rename the
            // previous file with an "_archived_<utc-ts>" suffix BEFORE writing the
            // new one. This keeps an audit trail on disk instead of letting
            // WriteAllBytes silently truncate the old payload. The DB column is
            // updated to the (stable) new path below, so archived files become
            // orphans intentionally — they are recoverable from the folder but
            // no longer referenced.
            if (attr.OverwriteExisting)
            {
                await ArchivePreviousFileIfAnyAsync(model, uploadProp, relativePath);
            }

            await secureFileStorage.WriteAsync(relativePath, finalBytes, HttpContext.RequestAborted);

            uploadProp.SetValue(model, relativePath);
        }

        /// <summary>
        /// Renames any existing file in the slot (current DB path and/or computed new path)
        /// with an "_archived_<utc-ts>" suffix. Best-effort: failures are swallowed so a
        /// rename hiccup never aborts the user's upload — the subsequent WriteAllBytes
        /// will still overwrite the original target.
        /// </summary>
        private async Task ArchivePreviousFileIfAnyAsync(
            object model,
            PropertyInfo uploadProp,
            string newRelativePath)
        {
            var pathsToArchive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) The target may already exist (typical case: same id+suffix).
            pathsToArchive.Add(newRelativePath);

            // 2) Path stored in the model/DB may live in a different subfolder
            //    (e.g. ByYearMonth strategy where the previous upload was in another month).
            var currentRelative = uploadProp.GetValue(model) as string;
            if (!string.IsNullOrWhiteSpace(currentRelative))
            {
                pathsToArchive.Add(currentRelative.Replace('\\', '/'));
            }

            foreach (var relativePath in pathsToArchive)
            {
                try
                {
                    await secureFileStorage.ArchiveIfExistsAsync(
                        relativePath,
                        HttpContext.RequestAborted);
                }
                catch (IOException)
                {
                    // Swallow: WriteAllBytes will overwrite the original target anyway.
                }
                catch (UnauthorizedAccessException)
                {
                    // Swallow: same reasoning — never block the user's upload on an audit-only rename.
                }
                catch (Exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
                {
                    // Archiving is best-effort for both local disk and remote providers.
                }
            }
        }

        private static string ResolveUploadSubfolderId(object model, Type modelType, FileUploadAttribute attr)
        {
            if (!string.IsNullOrWhiteSpace(attr.SubfolderEntityIdProperty))
            {
                var prop = modelType.GetProperty(
                    attr.SubfolderEntityIdProperty,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (prop != null)
                {
                    var val = prop.GetValue(model)?.ToString();
                    if (!string.IsNullOrWhiteSpace(val) && val != "0")
                        return val;
                }
            }

            return ResolveIdOrGuid(model, modelType);
        }

        private async Task EnsureFactoringInvoiceStorageContextAsync(object model, Type modelType)
        {
            var ownerIdProp = modelType.GetProperty(
                "CaseOwnerBankPartyId",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var ownerIndividualProp = modelType.GetProperty(
                "CaseOwnerIsIndividual",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var creditCaseIdProp = modelType.GetProperty(
                "CreditCaseId",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (ownerIdProp == null || creditCaseIdProp == null)
                return;

            var existingOwnerId = Convert.ToInt32(ownerIdProp.GetValue(model) ?? 0);
            var creditCaseId = Convert.ToInt64(creditCaseIdProp.GetValue(model) ?? 0L);
            if (creditCaseId <= 0)
                return;

            if (existingOwnerId > 0)
                return;

            var creditCaseService = serviceProvider.GetService(typeof(ICreditCaseService)) as ICreditCaseService;
            if (creditCaseService == null)
                return;

            var creditCase = await creditCaseService.GetByIdAsync(creditCaseId);
            if (creditCase.CreditCaseId <= 0 || creditCase.BankPartyId <= 0)
                throw new InvalidOperationException("Credit case nuk ka BankPartyId të vlefshëm për ngarkimin e faturës.");

            ownerIdProp.SetValue(model, creditCase.BankPartyId);

            if (ownerIndividualProp != null)
            {
                ownerIndividualProp.SetValue(model, creditCase.IsIndividual);
            }
        }

        private static string ResolveFactoringInvoiceFileNamePrefix(object model, Type modelType)
        {
            var factoringBankPartyId = Convert.ToInt32(
                modelType.GetProperty("FactoringBankPartyId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?.GetValue(model) ?? 0);
            if (factoringBankPartyId > 0)
                return factoringBankPartyId.ToString();

            var factoringAgreementId = Convert.ToInt32(
                modelType.GetProperty("FactoringAgreementId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?.GetValue(model) ?? 0);
            if (factoringAgreementId > 0)
                return factoringAgreementId.ToString();

            var creditCaseId = Convert.ToInt64(
                modelType.GetProperty("CreditCaseId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?.GetValue(model) ?? 0L);
            return creditCaseId > 0 ? creditCaseId.ToString() : "unknown";
        }

        private static string ResolveIdOrGuid(object model, Type modelType)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var pkProps = ModelHelper.GetPrimaryKeyProperties(modelType);

            if (pkProps.Length == 1)
            {
                var val = pkProps[0].GetValue(model);

                if (val is Guid g)
                    return g == Guid.Empty ? Guid.NewGuid().ToString("N") : g.ToString("N");

                var s = val?.ToString();
                return string.IsNullOrWhiteSpace(s) ? Guid.NewGuid().ToString("N") : s;
            }

            var parts = new string[pkProps.Length];
            for (int i = 0; i < pkProps.Length; i++)
            {
                var val = pkProps[i].GetValue(model);
                var s = val?.ToString();
                if (string.IsNullOrWhiteSpace(s))
                    return Guid.NewGuid().ToString("N");

                parts[i] = SanitizeFileName(s);
            }

            return string.Join("_", parts);
        }

        private static string EnsureHasExtensionFromPath(string fileName, string fullPath)
        {
            if (Path.HasExtension(fileName))
                return fileName;

            var ext = Path.GetExtension(fullPath);
            if (!string.IsNullOrWhiteSpace(ext))
                return fileName + ext;

            return fileName;
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            return fileName.Trim();
        }

        private static object? ConvertToPropertyType(object? value, Type propertyType)
        {
            if (value == null)
                return Nullable.GetUnderlyingType(propertyType) != null || !propertyType.IsValueType
                    ? null
                    : Activator.CreateInstance(propertyType);

            var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            if (targetType.IsEnum)
                return value is string enumString
                    ? Enum.Parse(targetType, enumString, true)
                    : Enum.ToObject(targetType, value);

            if (targetType == typeof(Guid))
                return value is Guid guid ? guid : Guid.Parse(value.ToString()!);

            return Convert.ChangeType(value, targetType);
        }

        #endregion Private Helpers
    }
}
