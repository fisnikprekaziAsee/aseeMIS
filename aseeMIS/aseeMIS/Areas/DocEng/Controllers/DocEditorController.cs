using dsMIS.Application.Utils;
using dsAppMng.Core.Services.Interfaces;
using dsMIS.Core.Attributes;
using dsMIS.Core.Enums;
using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using dsDocEng.Core.Services.Interfaces;
using dsDocEng.Core.ViewModels;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using System.Data;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Data;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Azure;
using System.Reflection;

namespace aseeMIS.Areas.DocEng.Controllers
{
    [Area("DocEng")]
    [Route("DocEng/DocEditor/[action]/{formId?}")]
    public class DocEditorController(
        IDocEngDocumentTemplateService templateService,
        IDocEngDocumentTemplateFieldService fieldService,
        IDocEngTemplateDataSourceService dataSourceService,
        IDocEngTemplateFieldMappingService fieldMappingService,
        IServiceProvider serviceProvider,
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IAppSessionService dsSessionService,
        IWebHostEnvironment env,
        IUserContextService userSecurityStampCacheService
    ) : Controller
    {


        /// <summary>
        /// Entry page for the Document Editor for a given form.
        /// - Validates READ permission (PARENT context).
        /// - Loads list of templates via ServiceInvoker (SelectAllAsync).
        /// - Stores FormSessionId in ViewBag + Response header for client requests.
        /// </summary>
        /// <param name="formId">AppForm id coming from route</param>
        /// <returns>DocumentEditor.cshtml view with template list</returns>
        /// <remarks>
        /// Called when user opens the DocEng editor page.
        /// The client will reuse FormSessionId in headers for LoadTemplate/SaveTemplate.
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> Index(int formId)
        {
            // Create/ensure a context once for this page load (PARENT to avoid child/parent drift)
            var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) =
                 await AppAuthorizeHelper.ValidateReadPermissionAsync(
                     HttpContext,
                     formAccessService,
                     menuService,
                     userSecurityStampCacheService,
                     formId,
                     EnumContextFormType.PARENT
                 );

            if (!isValid || contextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");


            try
            {
                string methodName = "SelectAllAsync";
                List<object> parameters = new();

                if (!string.IsNullOrEmpty(appForm.FormUIConfiguration))
                {
                    if (appForm.FormUIConfiguration.Contains("@UserId"))
                        parameters.Add(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

                    if (appForm.FormUIConfiguration.Contains("@LanguageCode"))
                        parameters.Add(dsSessionService.GetLanguageCode() ?? string.Empty);
                }

                var serviceInvoker = new ServiceInvoker(serviceProvider);
                var templates = await serviceInvoker.InvokeAsync(projectNamespace, serviceInterface, methodName, parameters.ToArray());

                ViewBag.Templates = templates;
                ViewBag.FormId = formId;
                ViewBag.FormSessionId = contextSessionDto.FormSessionId;
                ViewBag.FormConfigId = contextSessionDto.FormId;

                // Echo authoritative id so client can keep in sync if needed
                Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

                return View("~/Areas/DocEng/Views/DocumentEditor.cshtml", templates);
            }
            catch (AppException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("ShowError", "Error");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred.";
                return RedirectToAction("ShowError", "Error");
            }
        }

        #region ================= TEMPLATE LOADING (CLIENT REQUEST ? SFDT RESPONSE) =================

        /// <summary>
        /// Loads a template into the Syncfusion DocumentEditor.
        /// Flow:
        /// 1) Parse request payload (template id + mode + session).
        /// 2) Validate session for READ.
        /// 3) Build stored-procedure parameters (defaults + overrides).
        /// 4) Load template content either from FilePath or from TemplateBinary.
        /// 5) If mode=view -> map fields (SP + manual params) and return SFDT.
        /// </summary>
        /// <remarks>
        /// Called by frontend when user clicks "Edit/View" template.
        /// Returns JSON { sfdt }.
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> LoadTemplate([FromBody] JsonElement payload)
        {
            LoadTemplateRequest req;
            try
            {
                req = ParseLoadTemplateRequest(payload);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

            var v = await ValidateSessionAsync(req.FormSessionId, EumFormAccessAction.READ);
            if (!v.IsValid || v.ContextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");

            var parameters = await BuildParametersAsync(req.DocumentId, payload);

            var model = await templateService.GetByIdAsync(req.DocumentId);
            if (model == null)
                return NotFound("Document not found");

            var modelType = model.GetType();
            var resolved = ResolveFileUploadProps(modelType);
            if (resolved == null)
                return BadRequest("No FileUploadAttribute found");

            var filePath = ReadFilePath(model, resolved);

            if (!string.IsNullOrWhiteSpace(filePath))
                return await LoadFromFileSystem(filePath!, req.Mode, req.DocumentId, parameters);

            var bytes = ReadBinary(model, resolved);

            if (HasBinary(bytes))
                return await LoadFromDatabase(bytes!, req.Mode, req.DocumentId, parameters);

            return BadRequest("No file found for this template");
        }


        /// <summary>
        /// Loads template from filesystem (when FileUploadAttribute.IsFilePath is true).
        /// - Reads cached .sfdt.json if exists, otherwise converts DOCX to SFDT and caches.
        /// - If mode=view: reads DOCX bytes, maps fields, converts mapped DOCX to SFDT.
        /// </summary>
        /// <param name="filePath">Relative path saved in the model (e.g. /bpsUploads/...)</param>
        /// <param name="mode">"edit" or "view"</param>
        /// <param name="documentId">Template id</param>
        /// <param name="parameters">System/manual parameters used only in view mode</param>
        /// <returns>JSON { sfdt }</returns>
        /// <remarks>
        /// Called only from LoadTemplate() when filePath is available.
        /// </remarks>
        private async Task<IActionResult> LoadFromFileSystem(string filePath, string mode, int documentId, Dictionary<string, object>? parameters)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest("Invalid filePath");

            string sanitizedPath = filePath
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            string fullPath = Path.Combine(env.WebRootPath ?? "wwwroot", sanitizedPath);
            if (!System.IO.File.Exists(fullPath))
                return NotFound("File not found");

            string sfdtPath = Path.ChangeExtension(fullPath, ".sfdt.json");

            string sfdt;
            if (System.IO.File.Exists(sfdtPath))
            {
                sfdt = await System.IO.File.ReadAllTextAsync(sfdtPath);
            }
            else
            {
                sfdt = await ConvertDocxFilePathToSfdtAsync(fullPath);
                await System.IO.File.WriteAllTextAsync(sfdtPath, sfdt);
            }

            if (mode == "view")
            {
                byte[] docxBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                var mappedBytes = await MapTemplateFieldsAsync(docxBytes, documentId, false, parameters);
                sfdt = ConvertDocxToSfdt(mappedBytes);
            }

            return Json(new { sfdt });
        }


        /// <summary>
        /// Loads template content from DB binary.
        /// - Decompresses stored bytes (either raw docx zip OR zip containing template.docx).
        /// - If mode=view: maps fields using parameters.
        /// - Converts DOCX bytes to SFDT.
        /// </summary>
        /// <param name="binaryZipOrDocxBytes">TemplateBinary field contents</param>
        /// <param name="mode">"edit" or "view"</param>
        /// <param name="documentId">Template id</param>
        /// <param name="parameters">System/manual parameters used in mapping</param>
        /// <returns>JSON { sfdt }</returns>
        /// <remarks>
        /// Called only from LoadTemplate() when filePath is empty but binary exists.
        /// </remarks>
        private async Task<IActionResult> LoadFromDatabase(
            byte[] binaryZipOrDocxBytes,
            string mode,
            int documentId,
            Dictionary<string, object>? parameters)
        {
            var docxBytes = DecompressDocxFromZip(binaryZipOrDocxBytes);

            if (mode == "view")
                docxBytes = await MapTemplateFieldsAsync(docxBytes, documentId, false, parameters);

            var sfdt = ConvertDocxToSfdt(docxBytes);
            return Json(new { sfdt });
        }

        #endregion

        #region ================= DOCX ? SFDT CONVERSION (SYNCFUSION) =================

        /// <summary>
        /// Converts a DOCX file from disk into SFDT JSON (Syncfusion format).
        /// Used for caching: we store an .sfdt.json next to the docx for faster reload.
        /// </summary>
        /// <remarks>Called from LoadFromFileSystem() when sfdt cache doesn't exist.</remarks>
        private static async Task<string> ConvertDocxFilePathToSfdtAsync(string fullPath)
        {
            await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            using var wordDocument = new WordDocument(fileStream, FormatType.Docx);

            var sfdtDoc = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(wordDocument);
            return JsonConvert.SerializeObject(sfdtDoc);
        }

        /// <summary>
        /// Converts DOCX bytes into SFDT JSON (Syncfusion format).
        /// Used when we already have bytes in memory (from DB or after mapping).
        /// </summary>
        /// <remarks>Called from LoadFromDatabase() and LoadFromFileSystem(view mode).</remarks>
        private static string ConvertDocxToSfdt(byte[] docxBytes)
        {
            using var inputStream = new MemoryStream(docxBytes);
            using var wordDocument = new WordDocument(inputStream, FormatType.Docx);
            var documentEditor = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(wordDocument);
            string sfdt = JsonConvert.SerializeObject(documentEditor);
            return sfdt;
        }
        #endregion

        #region ================= CLIENT PAYLOAD PARSING & VALIDATION  =================

        private sealed record LoadTemplateRequest(int DocumentId, int FormId, string Mode, string? FormSessionId);

        /// <summary>
        /// Parses LoadTemplate payload:
        /// - Reads template id ("id"), formId, mode ("edit"/"view").
        /// - Reads FormSessionId from header or payload fallback.
        /// </summary>
        /// <remarks>
        /// Called only from LoadTemplate().
        /// Throws descriptive exception if payload is invalid.
        /// </remarks>
        private LoadTemplateRequest ParseLoadTemplateRequest(JsonElement payload)
        {
            var mode = payload.TryGetProperty("mode", out var modeElement)
                ? modeElement.GetString()?.ToLowerInvariant() ?? "edit"
                : "edit";

            int documentId = ReadInt(payload, "id");
            int formId = ReadInt(payload, "formId");

            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(formSessionId) &&
                payload.TryGetProperty("formSessionId", out var sidEl))
            {
                formSessionId = sidEl.GetString();
            }

            return new LoadTemplateRequest(documentId, formId, mode, formSessionId);
        }

        /// <summary>
        /// Reads int property from JsonElement accepting both number and string.
        /// </summary>
        /// <remarks>Used by ParseLoadTemplateRequest() and SaveTemplate().</remarks>
        private static int ReadInt(JsonElement payload, string propName)
        {
            if (!payload.TryGetProperty(propName, out var el))
                throw new Exception($"Missing '{propName}' in payload");

            return el.ValueKind switch
            {
                JsonValueKind.Number => el.GetInt32(),
                JsonValueKind.String when int.TryParse(el.GetString(), out var v) => v,
                _ => throw new Exception($"Invalid '{propName}' format")
            };
        }



        #endregion

        #region ================= FORM SESSION & PERMISSION VALIDATION =================


        private sealed record ValidatedContext(
            bool IsValid,
            object? AppForm,
            dynamic? ContextSessionDto,
            string? ProjectNamespace,
            string? ServiceInterface,
            string? ViewModel
        );

        /// <summary>
        /// Validates FormSessionId + permission using AppAuthorizeHelper.
        /// If valid, echoes authoritative FormSessionId back in response headers.
        /// </summary>
        /// <param name="formSessionId">Session id from client header/body</param>
        /// <param name="action">Permission required (READ/UPDATE)</param>
        /// <remarks>
        /// Used by LoadTemplate (READ) and SaveTemplate (UPDATE).
        /// Keeps client session in sync.
        /// </remarks>
        private async Task<ValidatedContext> ValidateSessionAsync(string? formSessionId, EumFormAccessAction action)
        {
            var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userSecurityStampCacheService,
                    formSessionId,
                    requiredPermission: action
                );

            if (isValid && contextSessionDto != null)
                Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

            return new ValidatedContext(isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel);
        }

        #endregion

        #region ================= TEMPLATE STORAGE RESOLUTION (FILE VS DATABASE) =================


        private sealed record FileUploadResolved(
            PropertyInfo PathProperty,
            FileUploadAttribute Attribute,
            PropertyInfo? BinaryProperty
        );

        /// <summary>
        /// Resolves the model property that has [FileUpload] attribute.
        /// Returns:
        /// - PathProperty: the annotated property (holds file path string)
        /// - Attribute: FileUploadAttribute itself
        /// - BinaryProperty: property referenced by BinaryFieldName (byte[])
        /// </summary>
        /// <remarks>
        /// Used by LoadTemplate() and SaveTemplate() to decide whether to use filesystem or DB.
        /// </remarks>
        private static FileUploadResolved? ResolveFileUploadProps(Type modelType)
        {
            var hit = modelType.GetProperties()
                .Select(p => new
                {
                    Property = p,
                    Attribute = p.GetCustomAttributes(typeof(FileUploadAttribute), true)
                                .FirstOrDefault() as FileUploadAttribute
                })
                .FirstOrDefault(x => x.Attribute != null);

            if (hit == null)
                return null;

            var attr = hit.Attribute!;
            PropertyInfo? binProp = null;

            if (!string.IsNullOrWhiteSpace(attr.BinaryFieldName))
                binProp = modelType.GetProperty(attr.BinaryFieldName);

            return new FileUploadResolved(hit.Property, attr, binProp);
        }

        /// <summary>
        /// Reads relative file path from the annotated property.
        /// </summary>
        private static string? ReadFilePath(object model, FileUploadResolved resolved)
            => resolved.PathProperty.GetValue(model) as string;

        /// <summary>
        /// Reads binary bytes from BinaryFieldName property.
        /// </summary>
        private static byte[]? ReadBinary(object model, FileUploadResolved resolved)
            => resolved.BinaryProperty?.GetValue(model) as byte[];

        /// <summary>
        /// Utility check for non-empty byte array.
        /// </summary>
        private static bool HasBinary(byte[]? bytes)
            => bytes is { Length: > 0 };

        #endregion

        #region ================= STORED PROCEDURE PARAMETER RESOLUTION =================

        /// <summary>
        /// Builds parameter dictionary for stored procedure execution.
        /// Sources:
        /// 1) defaults from datasource.InputParameters JSON
        /// 2) payload.parameters overrides (normalized to "@")
        /// 3) payload.entityPrimaryKeyValue => @EntityPrimaryKey
        /// </summary>
        /// <remarks>
        /// Called by LoadTemplate() and (optionally) SaveTemplate().
        /// Output is passed into MapTemplateFieldsAsync() for SP execution + manual replacements.
        /// </remarks>

        private async Task<Dictionary<string, object>> BuildParametersAsync(int documentId, JsonElement payload)
        {
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // 1) Find datasource
            var dataSourceId = await GetDataSourceIdForTemplateAsync(documentId);
            if (dataSourceId == 0)
                return parameters;

            var ds = await dataSourceService.GetByIdAsync(dataSourceId);
            if (ds == null || string.IsNullOrWhiteSpace(ds.InputParameters))
                return parameters;

            // 2) Read entity key parameter name from InputParameters
            var entityKeyParamName = GetEntityKeyParamName(ds.InputParameters);
            if (string.IsNullOrWhiteSpace(entityKeyParamName))
                return parameters;

            entityKeyParamName = NormalizeParam(entityKeyParamName);

            // 3) payload overrides (manual + explicit SP params)
            if (payload.TryGetProperty("parameters", out var paramsElement) &&
                paramsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in paramsElement.EnumerateObject())
                {
                    var key = (prop.Name ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    var value = ReadJsonValue(prop.Value);

                    // keep EXACTLY what caller sends
                    parameters[key.StartsWith("@") ? NormalizeParam(key) : key] = value;
                }
            }

            // 4) Bind entityPrimaryKeyValue to the dynamic SP parameter
            if (payload.TryGetProperty("entityPrimaryKeyValue", out var pkElement))
                parameters[entityKeyParamName] = ReadJsonValue(pkElement);

            return parameters;
        }



        /// <summary>
        /// Resolves entity key parameter name from datasource.InputParameters JSON.
        /// Source:
        /// 1) first parameter defined in InputParameters array
        /// </summary>
        /// <remarks>
        /// Used by BuildParametersAsync() to bind payload.entityPrimaryKeyValue
        /// to the correct stored procedure parameter.
        /// </remarks>
        private static string? GetEntityKeyParamName(string? inputParametersJson)
        {
            var ip = inputParametersJson?.Trim();

            // ? no params configured
            if (string.IsNullOrWhiteSpace(ip) ||
                ip.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                ip == "[]")
                return null;

            try
            {
                var arr = JArray.Parse(ip);
                if (arr.Count == 0)
                    return null;

                var name = arr[0]?["Name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return null;

                return NormalizeParam(name);
            }
            catch
            {
                // ? invalid JSON => treat as "no params"
                return null;
            }
        }



        /// <summary>
        /// Finds datasource id used by this template by joining:
        /// DocumentTemplateFields -> TemplateFieldMappings -> DataSourceId.
        /// </summary>
        /// <remarks>
        /// Used only by BuildParametersAsync() to locate datasource.InputParameters.
        /// </remarks>
        private async Task<int> GetDataSourceIdForTemplateAsync(int documentId)
        {
            // yes: still SelectAllAsync for now; we’ll optimize later by adding SelectByTemplateId in service
            var fields = await fieldService.SelectAllAsync();
            var mappings = await fieldMappingService.SelectAllAsync();

            var fieldIds = fields
                .Where(f => f.DocumentTemplateId == documentId)
                .Select(f => f.FieldId)
                .ToHashSet();

            return mappings
                .Where(m => fieldIds.Contains(m.FieldId))
                .Select(m => m.DataSourceId)
                .Where(id => id != 0)
                .Distinct()
                .FirstOrDefault();
        }

        #endregion

        #region ================= STORED PROCEDURE PARAMETER UTILITIES =================

        /// <summary>
        /// Merges datasource.InputParameters JSON (defaults) into target parameter dictionary.
        /// InputParameters example:
        /// [{ "Name":"@EntityPrimaryKey", "Type":"number", "DefaultValue":"1" }, ...]
        /// </summary>
        /// <remarks>Used inside BuildParametersAsync().</remarks>
        private static void MergeDatasourceDefaults(Dictionary<string, object> target, string? inputParametersJson)
        {
            var ip = inputParametersJson?.Trim();
            bool has = !string.IsNullOrWhiteSpace(ip)
                       && !ip.Equals("null", StringComparison.OrdinalIgnoreCase)
                       && ip != "[]";
            if (!has) return;

            var arr = JArray.Parse(ip);
            foreach (var item in arr)
            {
                var name = item["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var type = item["Type"]?.ToString();
                var def = item["DefaultValue"]?.ToString();

                target[NormalizeParam(name)] = ParseTypedDefault(type, def);
            }
        }

        /// <summary>
        /// Converts default value string into typed object based on Type.
        /// Returns DBNull.Value when DefaultValue is empty/null.
        /// </summary>
        private static object ParseTypedDefault(string? type, string? def)
        {
            if (string.IsNullOrWhiteSpace(def) || def.Equals("null", StringComparison.OrdinalIgnoreCase))
                return DBNull.Value;

            var t = type?.Trim().ToLowerInvariant();

            if (t == "number")
                return int.TryParse(def, out var n) ? n : (object)def;

            if (t is "bool" or "boolean")
                return bool.TryParse(def, out var b) ? b : (object)false;

            return def;
        }

        /// <summary>
        /// Normalizes parameter name to always start with '@'.
        /// </summary>
        private static string NormalizeParam(string name)
            => name.StartsWith("@", StringComparison.Ordinal) ? name : "@" + name;

        /// <summary>
        /// Converts JsonElement to simple CLR object for parameter dictionary.
        /// </summary>
        private static object ReadJsonValue(JsonElement el) =>
            el.ValueKind switch
            {
                JsonValueKind.Number => el.TryGetInt64(out var n) ? n : el.ToString()!,
                JsonValueKind.String => el.GetString() ?? "",
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => DBNull.Value
            };

        #endregion

        #region ===================== TEMPLATE FIELD METADATA (UI SUPPORT ENDPOINTS) =====================

        /// <summary>
        /// Returns list of template fields that require manual user input (non-System and active).
        /// Frontend uses this to show a popup/form for user-entered values.
        /// </summary>
        /// <remarks>
        /// Called before view-mode mapping so user can provide manual placeholders.
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> GetManualInputFields(int documentTemplateId)
        {
            // Fetch only the fields that belong to this document template
            var fields = await fieldService.SelectByTemplateId(documentTemplateId);
            if (fields == null || !fields.Any())
                return Json(new { hasManualInputs = false });

            // Filter: only fields that are NOT system-generated and active
            var manualFields = fields
                .Where(f => !string.Equals(f.FieldTypeCode, "System", StringComparison.OrdinalIgnoreCase) && f.IsActive)
                .ToList();

            // If no manual input fields, return false
            if (manualFields.Count == 0)
                return Json(new { hasManualInputs = false });

            // Otherwise return list of fields to prompt user
            return Json(new
            {
                hasManualInputs = true,
                fields = manualFields
            });
        }

        /// <summary>
        /// Returns all field names for a template (used for dropdown/autocomplete in UI).
        /// Echoes FormSessionId header back for client sync.
        /// </summary>
        /// <remarks>Used by UI to show selectable field placeholders.</remarks>
        [HttpGet]
        public IActionResult GetFieldNames(int templateId)
        {
            // (Optional) You can also echo the current FormSessionId for client sync:
            var sid = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(sid))
                Response.Headers["FormSessionId"] = sid;

            var fieldNames = fieldService.GetFieldNames(templateId);
            var list = fieldNames.Select(name => new { text = name }).ToList();
            return Json(list);
        }

        #endregion

        #region ===================== TEMPLATE SAVE (DOCX -> MAP -> SAVE) =====================

        /// <summary>
        /// Saves template content coming from client (DocumentEditor) as base64 DOCX.
        /// Flow:
        /// 1) Validate UPDATE permission.
        /// 2) Load template model using ServiceInvoker.
        /// 3) Resolve FileUploadAttribute mode (filesystem vs DB).
        /// 4) (Optional) MapTemplateFieldsAsync in edit mode (currently returns original bytes).
        /// 5) Save:
        ///    - filesystem: overwrite file
        ///    - DB: compress to zip and store in TemplateBinary
        /// </summary>
        /// <remarks>
        /// Called when user clicks "Save" in editor.
        /// Returns JSON { fileName, base64Content } for client.
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> SaveTemplate([FromBody] JsonElement payload)
        {
            if (!payload.TryGetProperty("templateId", out var idElement) ||
                !payload.TryGetProperty("fileBase64", out var fileBase64Element) ||
                !payload.TryGetProperty("formId", out var formIdElement))
                return BadRequest("Missing 'templateId', 'fileBase64', or 'formId'");

            int templateId = ReadInt(payload, "templateId");
            int formId = ReadInt(payload, "formId");

            string base64String = fileBase64Element.GetString() ?? "";

            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(base64String);
            }
            catch
            {
                return BadRequest("Invalid base64 file data.");
            }

            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var v = await ValidateSessionAsync(formSessionId, EumFormAccessAction.UPDATE);
            if (!v.IsValid || v.ContextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");

            var invoker = new ServiceInvoker(serviceProvider);

            var model = await invoker.InvokeAsync(
                v.ProjectNamespace!,
                v.ServiceInterface!,
                "GetByIdAsync",
                new object[] { templateId });

            if (model == null)
                return NotFound("Template not found");

            var modelType = model.GetType();

            var resolved = ResolveFileUploadProps(modelType);
            if (resolved == null)
                return BadRequest("No FileUploadAttribute found");

            var parameters = await BuildParametersAsync(templateId, payload);


            byte[] updatedDocBytes;
            try
            {
                updatedDocBytes = await MapTemplateFieldsAsync(fileBytes, templateId, true, parameters);
            }
            catch (Exception ex)
            {
                return BadRequest($"Mapping failed: {ex.Message}");
            }

            var attr = resolved.Attribute;

            var filePath = ReadFilePath(model, resolved);
            bool saveToFileSystem = attr.IsFilePath && !string.IsNullOrWhiteSpace(filePath);

            // ---------- FILESYSTEM ----------
            if (saveToFileSystem)
            {
                string fullPath = Path.Combine(env.WebRootPath ?? "wwwroot",
                    filePath!.TrimStart('/', '\\')
                             .Replace('/', Path.DirectorySeparatorChar)
                             .Replace('\\', Path.DirectorySeparatorChar));

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                await System.IO.File.WriteAllBytesAsync(fullPath, updatedDocBytes);

                return Ok(new
                {
                    fileName = Path.GetFileName(fullPath),
                    base64Content = Convert.ToBase64String(updatedDocBytes)
                });
            }

            // ---------- DATABASE (BINARY) ----------
            if (resolved.BinaryProperty == null || resolved.BinaryProperty.PropertyType != typeof(byte[]))
                return BadRequest($"Binary field '{attr.BinaryFieldName}' not found or not byte[].");

            byte[] compressed = CompressDocxToZip(updatedDocBytes);
            resolved.BinaryProperty.SetValue(model, compressed);

            await invoker.InvokeAsync(
                v.ProjectNamespace!,
                v.ServiceInterface!,
                "UpdateAsync",
                model,
                v.ContextSessionDto);

            string fileName = modelType.GetProperties()
                .FirstOrDefault(p => p.Name.ToLower().Contains("name") || p.Name.ToLower().Contains("title"))
                ?.GetValue(model)?.ToString() ?? $"Template_{templateId}.docx";

            return Ok(new
            {
                fileName,
                base64Content = Convert.ToBase64String(updatedDocBytes)
            });
        }

        #endregion

        #region ===================== TEMPLATE BINARY STORAGE FORMAT (ZIP ? DOCX) =====================

        /// <summary>
        /// Wraps DOCX bytes into a zip archive containing "template.docx".
        /// Used before saving to DB to keep consistent storage.
        /// </summary>
        private static byte[] CompressDocxToZip(byte[] docxBytes)
        {
            using var outputStream = new MemoryStream();
            using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("template.docx");
                using var entryStream = entry.Open();
                entryStream.Write(docxBytes, 0, docxBytes.Length);
            }
            return outputStream.ToArray();
        }

        /// <summary>
        /// Extracts DOCX bytes from storage.
        /// Supports:
        /// 1) raw DOCX (already a zip with word/ folder) => return as-is
        /// 2) zip that contains a .docx file => extract it
        /// </summary>
        /// <remarks>Used when reading TemplateBinary from DB.</remarks>
        private static byte[] DecompressDocxFromZip(byte[] zipBytes)
        {
            using var inputStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(inputStream, ZipArchiveMode.Read);

            // ?? Case 1: bytes already represent a DOCX (docx = zip with word/)
            if (archive.Entries.Any(e => e.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)))
                return zipBytes;

            // ?? Case 2: zip contains a .docx file
            var entry = archive.Entries
                .FirstOrDefault(e => e.FullName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                throw new InvalidOperationException("No DOCX file found in the zip archive.");

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            return ms.ToArray();
        }

        #endregion

        #region ===================== TEMPLATE DATA MAPPING (SP + MANUAL ? DOCX) =====================

        /// <summary>
        /// Maps stored procedure + manual parameters into DOCX placeholders.
        /// - Loads fields + mappings for template.
        /// - Executes single-value SP (ExecuteStoredProcedureAsync) using system (@) parameters.
        /// - Replaces placeholders in DOCX (doc.Replace).
        /// - If template has TableStart/TableEnd regions -> executes mail merge groups.
        /// </summary>
        /// <param name="docBytes">Original DOCX bytes</param>
        /// <param name="templateId">Template id</param>
        /// <param name="isEditMode">
        /// true: no replacements applied (editor mode).
        /// false: replacements + mail merge applied (view/print mode).
        /// </param>
        /// <param name="parameters">
        /// Built by BuildParametersAsync.
        /// Contains:
        /// - @params for stored procedure (system)
        /// - non-@ keys for manual replacements
        /// </param>
        /// <remarks>
        /// Called from LoadFromDatabase/LoadFromFileSystem when mode=view.
        /// (You can also call it from SaveTemplate if you ever want to store a mapped snapshot.)
        /// </remarks>
        private async Task<byte[]> MapTemplateFieldsAsync(
            byte[] docBytes,
            int templateId,
            bool isEditMode,
            Dictionary<string, object>? parameters = null)
        {
            // 1) Load template fields + mappings (reuse your existing helper)
            var (templateFields, templateMappings) = await LoadTemplateFieldsAndMappingsAsync(templateId);

            var hasTableRegions = templateFields.Any(f =>
                f.FieldName.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase));

            if (templateMappings.Count == 0 && !hasTableRegions)
                return docBytes;

            // 2) DataSourceId
            var dataSourceId = templateMappings.Select(m => m.DataSourceId).Distinct().FirstOrDefault();

            // 3) Split parameters (NO new helper: do it inline)
            var systemParams = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var manualParams = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    if (kv.Key.StartsWith("@", StringComparison.Ordinal))
                        systemParams[NormalizeParam(kv.Key)] = kv.Value ?? DBNull.Value;
                    else
                        manualParams[kv.Key] = kv.Value ?? "";
                }
            }

            // 4) Execute SP with system params
            Dictionary<string, object>? resultData = null;
            if (dataSourceId != 0)
                resultData = await dataSourceService.ExecuteStoredProcedureAsync(dataSourceId, systemParams);

            // 5) Build mappedResult (simple replacements only)
            var mappedResult = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (resultData != null && resultData.Count > 0)
            {
                var fieldById = templateFields.ToDictionary(f => f.FieldId);

                foreach (var mapping in templateMappings)
                {
                    if (!fieldById.TryGetValue(mapping.FieldId, out var field))
                        continue;

                    if (field.FieldName.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase)) continue;
                    if (field.FieldName.StartsWith("TableEnd:", StringComparison.OrdinalIgnoreCase)) continue;

                    mappedResult[field.FieldName] =
                        resultData.TryGetValue(mapping.SourceColumnName, out var val)
                            ? val?.ToString() ?? ""
                            : "";
                }
            }

            foreach (var kv in manualParams)
                mappedResult[kv.Key] = kv.Value?.ToString() ?? "";

            // Edit mode => do nothing to doc
            if (isEditMode)
                return docBytes;

            // 6) Apply replacements + regions
            using var stream = new MemoryStream(docBytes.Length + 500_000);
            await stream.WriteAsync(docBytes, 0, docBytes.Length);
            stream.Position = 0;

            using var doc = new WordDocument(stream, FormatType.Docx);

            foreach (var kv in mappedResult)
                doc.Replace($"{{{{{kv.Key}}}}}", kv.Value ?? "", true, false);

            if (hasTableRegions)
                await ApplyMailMergeRegionsAsync(doc, templateId, systemParams);


            HttpContext.Session.SetString("MappedFields", JsonConvert.SerializeObject(mappedResult));

            stream.SetLength(0);
            doc.Save(stream, FormatType.Docx);

            return stream.ToArray();
        }

        #endregion

        #region ===================== TABLE MAIL MERGE EXECUTION (REPEATING REGIONS) =====================

        /// <summary>
        /// Executes MailMerge regions (TableStart/TableEnd).
        /// Uses DB mappings to map merge fields -> SP table columns.
        /// </summary>
        /// <remarks>
        /// Called from MapTemplateFieldsAsync() when template contains table regions.
        /// </remarks>
        private async Task ApplyMailMergeRegionsAsync(WordDocument doc, int templateId, Dictionary<string, object>? systemParams = null)
        {
            // 1) Load all active fields + mappings for the template
            var (templateFields, templateMappings) = await LoadTemplateFieldsAndMappingsAsync(templateId);
            if (templateFields.Count == 0 || templateMappings.Count == 0)
                return;

            // 2) Discover region names (prefer DOCX groups; fallback to DB TableStart markers)
            var regionNames = DiscoverRegionNames(doc, templateFields);
            if (regionNames.Count == 0)
                return;

            // 3) Resolve datasource id (assumes one datasource per template; matches your current design)
            var dataSourceId = templateMappings.Select(m => m.DataSourceId).Distinct().FirstOrDefault();
            if (dataSourceId == 0)
                return;

            // 4) Execute SP returning DataTable (NO params — per your constraint)
            var spTable = await dataSourceService.ExecuteStoredProcedureTableAsync(dataSourceId, systemParams);
            if (spTable == null || spTable.Rows.Count == 0)
                return;

            // 5) Build lookup: mergeFieldName -> SPColumnName (based on DocEng mappings)
            // This is the "new thing" you were doing: FieldName -> SourceColumnName
            var spColumnByMergeField = BuildSpColumnLookup(templateFields, templateMappings);

            // 6) Run regions
            foreach (var region in regionNames)
            {
                // a) Find merge fields for THIS region if supported, else fallback to all merge fields
                var regionMergeFields = DiscoverRegionMergeFields(doc, region);

                // Remove TableStart/TableEnd markers + duplicates
                regionMergeFields = regionMergeFields
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Where(n => !n.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase))
                    .Where(n => !n.StartsWith("TableEnd:", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (regionMergeFields.Count == 0)
                    continue;

                // b) Build a DataTable whose column names match DOCX merge fields
                var dtRegion = BuildRegionTable(region, regionMergeFields, spTable, spColumnByMergeField);

                // c) Execute merge group
                doc.MailMerge.RemoveEmptyGroup = true;
                doc.MailMerge.ExecuteGroup(dtRegion);
            }
        }

        /// <summary>
        /// Loads active fields and their mappings for a template.
        /// Shared by MapTemplateFieldsAsync and ApplyMailMergeRegionsAsync.
        /// </summary>
        private async Task<(List<DocEngDocumentTemplateFieldViewModel> Fields, List<DocEngTemplateFieldMappingViewModel> Mappings)>
            LoadTemplateFieldsAndMappingsAsync(int templateId)
        {
            var allFields = await fieldService.SelectAllAsync();
            var allMappings = await fieldMappingService.SelectAllAsync();

            var fields = allFields
                .Where(f => f.DocumentTemplateId == templateId && f.IsActive)
                .ToList();

            var fieldIds = fields.Select(f => f.FieldId).ToHashSet();

            var mappings = allMappings
                .Where(m => fieldIds.Contains(m.FieldId))
                .ToList();

            return (fields, mappings);
        }

        /// <summary>
        /// Finds merge group names:
        /// - Prefer group names in DOCX.
        /// - Fallback to DB fields with "TableStart:Region".
        /// </summary>
        private static List<string> DiscoverRegionNames(WordDocument doc, List<DocEngDocumentTemplateFieldViewModel> templateFields)
        {
            // Prefer DOCX group names
            try
            {
                var groups = doc.MailMerge.GetMergeGroupNames();
                if (groups != null && groups.Length > 0)
                {
                    return groups
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch
            {
                // ignore, fallback below
            }

            // Fallback to DB markers: TableStart:RegionName
            return templateFields
                .Where(f => f.FieldName.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.FieldName.Split(':', 2)[1].Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }


        /// <summary>
        /// Gets merge field names for a region.
        /// Fallback to all merge fields if region-specific query fails.
        /// </summary>
        private static List<string> DiscoverRegionMergeFields(WordDocument doc, string regionName)
        {
            // Best: fields inside region
            try
            {
                var names = doc.MailMerge.GetMergeFieldNames(regionName);
                if (names != null && names.Length > 0)
                    return names.ToList();
            }
            catch
            {
                // ignore, fallback below
            }

            // Fallback: all merge fields from doc
            return (doc.MailMerge.GetMergeFieldNames() ?? Array.Empty<string>())
                .ToList();
        }

        /// <summary>
        /// Builds mapping dictionary: MergeFieldName (FieldName) -> SP ColumnName (SourceColumnName).
        /// </summary>
        private static Dictionary<string, string> BuildSpColumnLookup(
            List<DocEngDocumentTemplateFieldViewModel> templateFields,
            List<DocEngTemplateFieldMappingViewModel> templateMappings)
        {
            // FieldName -> SourceColumnName (first mapping wins)
            return templateFields
                .Join(templateMappings,
                      f => f.FieldId,
                      m => m.FieldId,
                      (f, m) => new { f.FieldName, m.SourceColumnName })
                .Where(x => !string.IsNullOrWhiteSpace(x.FieldName))
                .Where(x => !string.IsNullOrWhiteSpace(x.SourceColumnName))
                .GroupBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().SourceColumnName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a DataTable matching DOCX merge fields and fills it from SP result table.
        /// </summary>
        private static DataTable BuildRegionTable(
            string regionName,
            List<string> regionMergeFields,
            DataTable spTable,
            Dictionary<string, string> spColumnByMergeField)
        {
            var dtRegion = new DataTable(regionName);

            // Columns must match merge fields in DOCX
            foreach (var mergeField in regionMergeFields)
                dtRegion.Columns.Add(mergeField);

            // Fill rows from SP table
            foreach (DataRow spRow in spTable.Rows)
            {
                var newRow = dtRegion.NewRow();

                foreach (var mergeField in regionMergeFields)
                {
                    if (!spColumnByMergeField.TryGetValue(mergeField, out var spCol) ||
                        string.IsNullOrWhiteSpace(spCol) ||
                        !spTable.Columns.Contains(spCol))
                    {
                        newRow[mergeField] = DBNull.Value;
                        continue;
                    }

                    var value = spRow[spCol];

                    if (value == null || value == DBNull.Value)
                    {
                        newRow[mergeField] = DBNull.Value;
                    }
                    else if (value is DateOnly dateOnly)
                    {
                        newRow[mergeField] = dateOnly.ToString("dd.MM.yyyy");
                    }
                    else if (value is DateTime dateTime)
                    {
                        newRow[mergeField] = dateTime.ToString("dd.MM.yyyy");
                    }
                    else
                    {
                        newRow[mergeField] = value;
                    }
                }

                dtRegion.Rows.Add(newRow);
            }

            return dtRegion;
        }

        #endregion

    }
}







#region =====================  COMMENTED CODEE NOT USED !!!! TEMPLATE OPEN / LOAD (BINARY -> DOCX -> SFDT) =====================

//[HttpGet]
//public async Task<IActionResult> OpenTemplate(int documentTemplateId)
//{
//    try
//    {
//        // 1?? Fetch template
//        var document = await templateService.GetByIdAsync(documentTemplateId);
//        if (document == null)
//            return NotFound("Document not found.");

//        // 2?? Decompress if binary is zipped
//        var compressed = document.TemplateBinary;
//        if (compressed == null || compressed.Length == 0)
//            return BadRequest("No document content available.");

//        var docxBytes = DecompressDocxFromZip(compressed);

//        // 3?? Convert DOCX to SFDT (ConvertDocxToSfdt() returns an object)
//        var sfdtObj = ConvertDocxToSfdt(docxBytes);

//        // ? Make sure it becomes a string before passing to the view
//        string sfdtString = JsonConvert.SerializeObject(sfdtObj);

//        // 4?? Pass to partial view
//        ViewBag.Sfdt = sfdtString;
//        ViewBag.DocumentName = document.DocumentTemplateName ?? "Document";

//        return PartialView("~/Areas/DocEng/Views/_DocumentViewer.cshtml");
//    }
//    catch (Exception ex)
//    {
//        return BadRequest($"Error opening template: {ex.Message}");
//    }
//}



//private static string ConvertDocxFileToSfdt(Stream stream)
//{
//    using var wordDocument = new WordDocument(stream, FormatType.Docx);
//    var documentEditor = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(wordDocument);
//    return JsonConvert.SerializeObject(documentEditor);
//}




//[HttpPost]
//public async Task<IActionResult> OpenTemplate([FromBody] JsonElement payload)
//{
//    // Parse mode
//    string mode = payload.TryGetProperty("mode", out var modeElement)
//        ? modeElement.GetString()?.ToLower() ?? "edit"
//        : "edit";

//    // Ensure payload has id + formId
//    if (!payload.TryGetProperty("id", out var idElement) ||
//        !payload.TryGetProperty("formId", out var formIdElement))
//        return BadRequest("Missing 'id' or 'formId' in payload");

//    // ? Handle both string or numeric types safely
//    int documentId = idElement.ValueKind switch
//    {
//        JsonValueKind.Number => idElement.GetInt32(),
//        JsonValueKind.String when int.TryParse(idElement.GetString(), out var parsedId) => parsedId,
//        _ => throw new Exception("Invalid 'id' format")
//    };

//    int formId = formIdElement.ValueKind switch
//    {
//        JsonValueKind.Number => formIdElement.GetInt32(),
//        JsonValueKind.String when int.TryParse(formIdElement.GetString(), out var parsedFormId) => parsedFormId,
//        _ => throw new Exception("Invalid 'formId' format")
//    };

//    // ? Handle optional parameters dynamically
//    Dictionary<string, object>? parameters = null;

//    // Case 1??: Full "parameters" object
//    if (payload.TryGetProperty("parameters", out var paramsElement) && paramsElement.ValueKind == JsonValueKind.Object)
//    {
//        parameters = new Dictionary<string, object>();
//        foreach (var prop in paramsElement.EnumerateObject())
//        {
//            object value = prop.Value.ValueKind switch
//            {
//                JsonValueKind.Number => prop.Value.TryGetInt64(out var n) ? n : prop.Value.ToString(),
//                JsonValueKind.String => prop.Value.GetString(),
//                JsonValueKind.True => true,
//                JsonValueKind.False => false,
//                _ => DBNull.Value
//            };
//            parameters[prop.Name.StartsWith("@") ? prop.Name : "@" + prop.Name] = value ?? DBNull.Value;
//        }
//    }
//    // Case 2??: Only "entityPrimaryKeyValue" provided
//    else if (payload.TryGetProperty("entityPrimaryKeyValue", out var pkElement))
//    {
//        object pkValue = pkElement.ValueKind switch
//        {
//            JsonValueKind.Number => pkElement.GetInt32(),
//            JsonValueKind.String => pkElement.GetString() ?? "",
//            _ => DBNull.Value
//        };

//        parameters = new Dictionary<string, object>
//        {
//            { "@EntityPrimaryKey", pkValue }
//        };
//    }

//    // ? Reuse existing session by header only
//    var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
//    if (string.IsNullOrWhiteSpace(formSessionId) && payload.TryGetProperty("formSessionId", out var formSessionElement))
//    {
//        formSessionId = formSessionElement.GetString();
//    }

//    var (isValid,
//         appForm,
//         contextSessionDto,
//         projectNamespace,
//         serviceInterface,
//         viewModel) =
//        await AppAuthorizeHelper.ValidateActionAsync(
//            HttpContext,
//            formAccessService,
//            menuService,
//            userSecurityStampCacheService,
//            formSessionId,
//            requiredPermission: EumFormAccessAction.READ
//        );

//    if (!isValid || appForm == null || contextSessionDto == null)
//        return RedirectToAction("NoAccess", "Error");

//    // Always echo authoritative session id back
//    Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

//    // --- Load the document via direct service call ---
//    var model = await templateService.GetByIdAsync(documentId);
//    if (model == null)
//        return NotFound("Document not found");

//    var modelType = typeof(DocEngDocumentTemplateViewModel);

//    // Find file upload props
//    var fileUploadProps = modelType.GetProperties()
//        .Select(p => new
//        {
//            Property = p,
//            Attribute = p.GetCustomAttributes(typeof(FileUploadAttribute), true)
//                         .FirstOrDefault() as FileUploadAttribute
//        })
//        .Where(x => x.Attribute != null)
//        .ToList();

//    var matchingFileProp = fileUploadProps.FirstOrDefault(x =>
//    {
//        var value = x.Property.GetValue(model);
//        return value != null && (
//            (x.Attribute!.IsFilePath && value is string s && !string.IsNullOrWhiteSpace(s)) ||
//            (!x.Attribute!.IsFilePath && value is byte[] b && b.Length > 0)
//        );
//    });

//    if (matchingFileProp == null)
//        return BadRequest("No file found for this template");

//    // ? We'll forward parameters later (in LoadFromDatabase)
//    string sfdt;

//    if (matchingFileProp.Attribute!.IsFilePath)
//    {
//        string filePath = matchingFileProp.Property.GetValue(model)?.ToString() ?? "";
//        var result = await LoadFromFileSystem(filePath, mode, documentId) as JsonResult;
//        sfdt = result?.Value?.GetType().GetProperty("sfdt")?.GetValue(result.Value)?.ToString() ?? "";
//    }
//    else
//    {
//        var result = await LoadFromDatabase(documentId, formId, mode, parameters) as JsonResult;
//        sfdt = result?.Value?.GetType().GetProperty("sfdt")?.GetValue(result.Value)?.ToString() ?? "";
//    }

//    if (string.IsNullOrWhiteSpace(sfdt))
//        return BadRequest("Failed to load document content.");

//    // ? Pass sfdt to the Document Viewer partial view
//    ViewBag.Sfdt = sfdt;
//    ViewBag.DocumentName = $"Document_{documentId}";
//    return PartialView("_DocumentViewer");
//}





//[HttpPost]
//public async Task<IActionResult> LoadTemplate([FromBody] JsonElement payload)
//{
//    // Parse mode
//    string mode = payload.TryGetProperty("mode", out var modeElement)
//        ? modeElement.GetString()?.ToLower() ?? "edit"
//        : "edit";

//    // Ensure payload has id + formId
//    if (!payload.TryGetProperty("id", out var idElement) ||
//        !payload.TryGetProperty("formId", out var formIdElement))
//        return BadRequest("Missing 'id' or 'formId' in payload");

//    // ? Handle both string or numeric types safely
//    int documentId = idElement.ValueKind switch
//    {
//        JsonValueKind.Number => idElement.GetInt32(),
//        JsonValueKind.String when int.TryParse(idElement.GetString(), out var parsedId) => parsedId,
//        _ => throw new Exception("Invalid 'id' format")
//    };

//    int formId = formIdElement.ValueKind switch
//    {
//        JsonValueKind.Number => formIdElement.GetInt32(),
//        JsonValueKind.String when int.TryParse(formIdElement.GetString(), out var parsedFormId) => parsedFormId,
//        _ => throw new Exception("Invalid 'formId' format")
//    };

//    // ---- mappings (you already had this) ----
//    var fields = await fieldService.SelectAllAsync();
//    var mappings = await fieldMappingService.SelectAllAsync();

//    var templateFields = fields.Where(f => f.DocumentTemplateId == documentId).ToList();
//    var fieldIds = templateFields.Select(f => f.FieldId).ToList();

//    var templateMappings = mappings.Where(m => fieldIds.Contains(m.FieldId)).ToList();

//    // ? Build SP parameters
//    Dictionary<string, object> parameters = new();

//    // 1) datasource from mappings
//    var dataSourceId = templateMappings
//        .Select(m => m.DataSourceId)
//        .Where(id => id != 0)
//        .Distinct()
//        .FirstOrDefault();

//    if (dataSourceId != 0)
//    {
//        // ? do NOT SelectAllAsync; fetch only the needed datasource
//        var ds = await dataSourceService.GetByIdAsync(dataSourceId);

//        // ? Only run if InputParameters is REAL (not NULL, not "", not "null", not "[]")
//        var ip = ds?.InputParameters?.Trim();

//        bool hasInputParams =
//            !string.IsNullOrWhiteSpace(ip) &&
//            !ip.Equals("null", StringComparison.OrdinalIgnoreCase) &&
//            ip != "[]";

//        if (hasInputParams)
//        {
//            var arr = Newtonsoft.Json.Linq.JArray.Parse(ip);

//            // if someone stored "[]" but spaced, this also handles it
//            if (arr.Count > 0)
//            {
//                foreach (var item in arr)
//                {
//                    var name = item["Name"]?.ToString();
//                    if (string.IsNullOrWhiteSpace(name)) continue;

//                    var type = item["Type"]?.ToString();
//                    var def = item["DefaultValue"]?.ToString();

//                    object value;

//                    if (string.IsNullOrWhiteSpace(def) ||
//                        string.Equals(def, "null", StringComparison.OrdinalIgnoreCase))
//                    {
//                        value = DBNull.Value;
//                    }
//                    else
//                    {
//                        var t = type?.Trim().ToLowerInvariant();

//                        if (t == "number")
//                            value = int.TryParse(def, out var n) ? n : (object)def;
//                        else if (t == "bool" || t == "boolean")
//                            value = bool.TryParse(def, out var b) ? b : (object)false;
//                        else
//                            value = def;
//                    }

//                    // ? normalize: always with @
//                    var key = name.StartsWith("@") ? name : "@" + name;
//                    parameters[key] = value ?? DBNull.Value;
//                }
//            }
//        }
//        // else: datasource has NO input params -> skip, parameters stays empty here
//    }

//    // 2) OPTIONAL: allow payload to override datasource defaults
//    if (payload.TryGetProperty("parameters", out var paramsElement) &&
//        paramsElement.ValueKind == JsonValueKind.Object)
//    {
//        foreach (var prop in paramsElement.EnumerateObject())
//        {
//            object value = prop.Value.ValueKind switch
//            {
//                JsonValueKind.Number => prop.Value.TryGetInt64(out var n) ? n : prop.Value.ToString(),
//                JsonValueKind.String => prop.Value.GetString(),
//                JsonValueKind.True => true,
//                JsonValueKind.False => false,
//                _ => DBNull.Value
//            };

//            var key = prop.Name.StartsWith("@") ? prop.Name : "@" + prop.Name;
//            parameters[key] = value ?? DBNull.Value;
//        }
//    }

//    // 3) system param from entityPrimaryKeyValue
//    if (payload.TryGetProperty("entityPrimaryKeyValue", out var pkElement))
//    {
//        object pkValue = pkElement.ValueKind switch
//        {
//            JsonValueKind.Number => pkElement.GetInt32(),
//            JsonValueKind.String => pkElement.GetString() ?? "",
//            _ => DBNull.Value
//        };

//        parameters["@EntityPrimaryKey"] = pkValue;
//    }

//    // ? Reuse existing session by header only
//    var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
//    if (string.IsNullOrWhiteSpace(formSessionId) &&
//        payload.TryGetProperty("formSessionId", out var formSessionElement))
//    {
//        formSessionId = formSessionElement.GetString();
//    }


//    var (isValid,
//         appForm,
//         contextSessionDto,
//         projectNamespace,
//         serviceInterface,
//         viewModel) =
//        await AppAuthorizeHelper.ValidateActionAsync(
//            HttpContext,
//            formAccessService,
//            menuService,
//            userSecurityStampCacheService,
//            formSessionId,
//            requiredPermission: EumFormAccessAction.READ
//        );

//    if (!isValid || appForm == null || contextSessionDto == null)
//        return RedirectToAction("NoAccess", "Error");

//    // Always echo authoritative session id back
//    Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;


//    var model = await templateService.GetByIdAsync(documentId);

//    if (model == null)
//        return NotFound("Document not found");

//    //var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModel);
//    var modelType = typeof(DocEngDocumentTemplateViewModel);

//    // Find file upload props
//    var fileUploadProps = modelType.GetProperties()
//        .Select(p => new
//        {
//            Property = p,
//            Attribute = p.GetCustomAttributes(typeof(FileUploadAttribute), true)
//                         .FirstOrDefault() as FileUploadAttribute
//        })
//        .Where(x => x.Attribute != null)
//        .ToList();
//    // Find the FIRST file-upload attributed property that has either a path or binary present
//    var matchingFileProp = fileUploadProps.FirstOrDefault(x =>
//    {
//        var value = x.Property.GetValue(model);

//        // Case A: path present on the main property
//        if (value is string s && !string.IsNullOrWhiteSpace(s))
//            return true;

//        // Case B: binary present on BinaryFieldName (fallback / legacy / mixed storage)
//        var binFieldName = x.Attribute!.BinaryFieldName;
//        if (!string.IsNullOrWhiteSpace(binFieldName))
//        {
//            var binProp = modelType.GetProperty(binFieldName);
//            var binVal = binProp?.GetValue(model) as byte[];
//            if (binVal != null && binVal.Length > 0)
//                return true;
//        }

//        return false;
//    });

//    if (matchingFileProp == null)
//        return BadRequest("No file found for this template");

//    // ? Decide load source by REAL data, not only IsFilePath
//    var filePathValue = matchingFileProp.Property.GetValue(model) as string;
//    var binaryFieldName = matchingFileProp.Attribute!.BinaryFieldName;

//    byte[]? binaryValue = null;
//    if (!string.IsNullOrWhiteSpace(binaryFieldName))
//    {
//        var binProp = modelType.GetProperty(binaryFieldName);
//        binaryValue = binProp?.GetValue(model) as byte[];
//    }

//    // Prefer path if it exists
//    if (!string.IsNullOrWhiteSpace(filePathValue))
//    {
//        return await LoadFromFileSystem(filePathValue, mode, documentId);
//    }

//    // Otherwise fallback to binary
//    if (binaryValue != null && binaryValue.Length > 0)
//    {
//        return await LoadFromDatabase(documentId, formId, mode, parameters);
//    }

//    return BadRequest("No file found for this template");

//}


//private string RestoreMergeFields(string sfdt, int templateId)
//{
//    var fieldNames = fieldService.GetFieldNames(templateId);

//    foreach (var field in fieldNames)
//    {
//        string placeholder = $"«{field}»";

//        var fieldValue = GetReplacedValue(field, templateId);
//        if (!string.IsNullOrWhiteSpace(fieldValue) && sfdt.Contains(fieldValue))
//        {
//            sfdt = sfdt.Replace(fieldValue, placeholder);
//        }
//    }

//    return sfdt;
//}

//private string GetReplacedValue(string field, int templateId)
//{
//    var json = HttpContext.Session.GetString($"MappedFields");
//    if (string.IsNullOrWhiteSpace(json)) return null;

//    var mapped = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
//    if (mapped == null) return null;

//    return mapped.TryGetValue(field, out var value) ? value : null;
//}

//private async Task<IActionResult> LoadFromDatabase(int documentId, int formId, string mode, Dictionary<string, object>? parameters = null)
//{


//    var serviceInvoker = new ServiceInvoker(serviceProvider);
//    //var model = await serviceInvoker.InvokeAsync(projectNamespace, serviceInterface, "GetByIdAsync", new object[] { documentId });
//    var model = await templateService.GetByIdAsync(documentId);

//    if (model == null)
//        return NotFound("Document not found");

//    //var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModel);
//    var modelType = typeof(DocEngDocumentTemplateViewModel);

//    // ? FIX: find FileUploadAttribute (usually on the string path property)
//    var fileUploadProp = modelType.GetProperties()
//        .Select(p => new
//        {
//            Prop = p,
//            Attr = p.GetCustomAttributes(typeof(FileUploadAttribute), true)
//                    .FirstOrDefault() as FileUploadAttribute
//        })
//        .FirstOrDefault(x => x.Attr != null);

//    if (fileUploadProp == null)
//        return BadRequest("No FileUploadAttribute found");

//    // ? FIX: get the binary field name from the attribute (e.g. TemplateBinary)
//    var binaryFieldName = fileUploadProp.Attr!.BinaryFieldName;

//    if (string.IsNullOrWhiteSpace(binaryFieldName))
//        return BadRequest("BinaryFieldName is not set on FileUploadAttribute");

//    // ? FIX: get the actual byte[] property by name
//    var binaryProp = modelType.GetProperty(binaryFieldName);

//    if (binaryProp == null || binaryProp.PropertyType != typeof(byte[]))
//        return BadRequest($"Binary field '{binaryFieldName}' not found or not byte[]");

//    var binaryData = binaryProp.GetValue(model) as byte[];
//    if (binaryData == null || binaryData.Length == 0)
//        return BadRequest("Binary data is empty");


//    var docxBytes = DecompressDocxFromZip(binaryData);

//    if (mode == "view")
//    {
//        // ? Forward parameters here
//        docxBytes = await MapTemplateFieldsAsync(docxBytes, documentId, false, parameters);
//    }

//    var sfdt = ConvertDocxToSfdt(docxBytes);
//    return Json(new { sfdt });
//}





//private async Task<byte[]> MapTemplateFieldsAsync(
//    byte[] docBytes,
//    int templateId,
//    bool isEditMode,
//    Dictionary<string, object>? parameters = null)
//{
//    var fields = await fieldService.SelectAllAsync();
//    var mappings = await fieldMappingService.SelectAllAsync();

//    var templateFields = fields
//        .Where(f => f.DocumentTemplateId == templateId)
//        .ToList();

//    var fieldIds = templateFields.Select(f => f.FieldId).ToList();

//    var templateMappings = mappings
//        .Where(m => fieldIds.Contains(m.FieldId))
//        .ToList();

//    // ? detect if this template uses MailMerge regions (TableStart/TableEnd)
//    var hasTableRegions = templateFields.Any(f =>
//        f.FieldName.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase));

//    // if no mappings AND no regions => nothing to do
//    if (templateMappings.Count == 0 && !hasTableRegions)
//        return docBytes;

//    // ?? Get data source id from mappings
//    var dataSourceId = templateMappings
//        .Select(m => m.DataSourceId)
//        .Distinct()
//        .FirstOrDefault();

//    // ? Manual params = non-@ keys (simple replacements)
//    var manualParams = parameters?
//        .Where(p => !p.Key.StartsWith("@"))
//        .ToDictionary(p => p.Key, p => p.Value)
//        ?? new Dictionary<string, object>();

//    // ? Incoming overrides normalized (ensure '@')
//    var incomingSystemOverrides = parameters?
//        .Where(p => p.Key.StartsWith("@"))
//        .ToDictionary(p => p.Key, p => p.Value)
//        ?? new Dictionary<string, object>();

//    // ? system params will come FROM datasource.InputParameters
//    var systemParams = new Dictionary<string, object>();

//    if (dataSourceId != 0)
//    {
//        // 1) SELECT datasource
//        var ds = await dataSourceService.GetByIdAsync(dataSourceId);

//        // ? Only run if InputParameters is REAL (not NULL, not "", not "null", not "[]")
//        var ip = ds?.InputParameters?.Trim();

//        bool hasInputParams =
//            !string.IsNullOrWhiteSpace(ip) &&
//            !ip.Equals("null", StringComparison.OrdinalIgnoreCase) &&
//            ip != "[]";

//        if (hasInputParams)
//        {
//            // Example JSON: [{"Name":"@EntityPrimaryKey","Type":"number","DefaultValue":"1"}, ...]
//            var arr = JArray.Parse(ip);

//            foreach (var item in arr)
//            {
//                var name = item["Name"]?.ToString();
//                if (string.IsNullOrWhiteSpace(name))
//                    continue;

//                // default value from datasource config
//                var def = item["DefaultValue"]?.ToString();
//                var type = item["Type"]?.ToString();

//                object value;

//                // 3) override: if caller passed @ParamName use it
//                if (incomingSystemOverrides.TryGetValue(name, out var overrideVal) &&
//                    overrideVal != null && overrideVal != DBNull.Value)
//                {
//                    value = overrideVal;
//                }
//                else
//                {
//                    // 4) otherwise use default from datasource config
//                    if (string.IsNullOrWhiteSpace(def) ||
//                        string.Equals(def, "null", StringComparison.OrdinalIgnoreCase))
//                    {
//                        value = DBNull.Value;
//                    }
//                    else
//                    {
//                        // minimal conversion without DTO
//                        var t = type?.Trim().ToLowerInvariant();
//                        if (t == "number")
//                        {
//                            value = int.TryParse(def, out var n) ? n : (object)def;
//                        }
//                        else if (t == "boolean" || t == "bool")
//                        {
//                            value = bool.TryParse(def, out var b) ? b : (object)false;
//                        }
//                        else
//                        {
//                            value = def; // string
//                        }
//                    }
//                }

//                systemParams[name] = value;
//            }
//        }
//    }

//    Dictionary<string, object>? resultData = null;

//    // ? FIX: execute single-value SP only if we have datasource AND system params
//    if (dataSourceId != 0 && systemParams.Count > 0)
//    {
//        var dataSource = await dataSourceService.GetByIdAsync(dataSourceId);
//        if (dataSource != null)
//        {
//            resultData = await dataSourceService.ExecuteStoredProcedureAsync(dataSourceId, systemParams);
//        }
//    }

//    // ?? Merge both types into one unified dictionary (simple replacements only)
//    var mappedResult = new Dictionary<string, string>();

//    // 1?? Map system values from stored procedure
//    if (resultData != null && resultData.Count > 0)
//    {
//        foreach (var mapping in templateMappings)
//        {
//            var field = templateFields.FirstOrDefault(f => f.FieldId == mapping.FieldId);
//            if (field == null) continue;

//            // table markers are NOT simple replacements
//            if (field.FieldName.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase)) continue;
//            if (field.FieldName.StartsWith("TableEnd:", StringComparison.OrdinalIgnoreCase)) continue;

//            string value = resultData.TryGetValue(mapping.SourceColumnName, out var val)
//                ? val?.ToString() ?? ""
//                : "";

//            mappedResult[field.FieldName] = value;
//        }
//    }

//    // 2?? Add manual parameters directly (user input) (simple replacements)
//    foreach (var kvp in manualParams)
//    {
//        mappedResult[kvp.Key] = kvp.Value?.ToString() ?? "";
//    }

//    // If edit mode: do NOT replace / do NOT merge regions
//    if (isEditMode)
//        return docBytes;

//    using var stream = new MemoryStream(docBytes.Length + 500_000);
//    await stream.WriteAsync(docBytes, 0, docBytes.Length);
//    stream.Position = 0;

//    using var doc = new WordDocument(stream, FormatType.Docx);

//    // ? Apply simple replacements (only if we have any)
//    if (mappedResult.Count > 0)
//    {
//        foreach (var kvp in mappedResult)
//        {
//            doc.Replace(kvp.Key, kvp.Value ?? "", true, false);
//        }
//    }

//    // ? Apply table regions ONLY if template contains TableStart: fields
//    // and only if system params exist (otherwise SP can't run)
//    if (hasTableRegions || systemParams.Count > 0)
//    {
//        await ApplyMailMergeRegionsAsync(doc, templateId);
//    }

//    // Clean placeholders
//    doc.Replace("»«", " ", true, false);
//    doc.Replace("«", "", true, false);
//    doc.Replace("»", "", true, false);

//    HttpContext.Session.SetString("MappedFields", JsonConvert.SerializeObject(mappedResult));

//    stream.SetLength(0);
//    doc.Save(stream, FormatType.Docx);

//    return stream.ToArray();
//}

#endregion



