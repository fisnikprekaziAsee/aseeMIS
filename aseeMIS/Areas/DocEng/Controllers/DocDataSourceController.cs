using dsMIS.Application.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using dsDocEng.Core.Services.Interfaces;
using dsDocEng.Core.ViewModels;
using dsAppMng.Core.Services;
using System.Security.Claims;
using dsAppMng.Core.Services.Interfaces;
using Microsoft.AspNetCore.Connections;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Data;
using Microsoft.Data.SqlClient;

using aseeMIS.Utils;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Services.Interfaces;
using System.Text.Json;
using dsMIS.Core.Enums;
using dsAppMng.Core.ViewModels;

namespace aseeMIS.Areas.DocEng.Controllers
{
    [Area("DocEng")]
    [Route("DocEng/DocDataSource/[action]/{formId?}")]
    public class DocDataSourceController(IDocEngTemplateDataSourceService dataSourceService, IServiceProvider serviceProvider, IAppConstantService appConstantService, IAppFormMenuService menuService, IDocEngSqlConnectionFactory dbConnectionFactory, IAppFormPermissionService formAccessService,
    IAppFormService<AppFormViewModel> appFormService, IUserContextService userSecurityStampCacheService, IAppSessionService sessionService) : Controller
    {

        [HttpGet]
        public async Task<IActionResult> Index(int formId)
        {
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
          await AppAuthorizeHelper.ValidateReadPermissionAsync(
              HttpContext,
              
              formAccessService,
              menuService,
              userSecurityStampCacheService, // <-- new arg
              formId,
              EnumContextFormType.PARENT       // <-- new arg
          );

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            if (!isValid || serviceInterface == null)
                return Unauthorized("Access denied");

            string selectedDataSourceType = Request.Query["selectedDataSourceType"].ToString();

            var serviceInvoker = new ServiceInvoker(serviceProvider);
            var allDataSources = await serviceInvoker.InvokeAsync(projectNamespace, serviceInterface, "SelectAllAsync", []);

            var dataSources = ((IEnumerable<object>)allDataSources)
                .Where(ds => string.IsNullOrEmpty(selectedDataSourceType) ||
                             ds?.GetType().GetProperty("DataSourceType")?.GetValue(ds)?.ToString() == selectedDataSourceType)
                .ToList();

            var filterOptions = await appConstantService.SelectForDropdownAsync("DocEngTemplateDataSource", "sq-AL");

            ViewBag.DataSourceType = filterOptions;
            ViewBag.SelectedDataSourceType = selectedDataSourceType;
            ViewBag.FormId = formId;

            var typedList = ((IEnumerable<object>)allDataSources)
            .Cast<DocEngTemplateDataSourceViewModel>()
            .Where(ds => string.IsNullOrEmpty(selectedDataSourceType) ||
                         ds.DataSourceType == selectedDataSourceType)
            .ToList();

            return View("~/Areas/DocEng/Views/DocumentDataSource.cshtml", typedList);

            //   return View("~/Areas/DocEng/Views/DocumentDataSource.cshtml", dataSources);
        }


        [HttpGet]
        public async Task<IActionResult> GetStoredProcedureParameters(string procedureName)
        {
            if (string.IsNullOrEmpty(procedureName))
            {
                return BadRequest(new { success = false, message = "Procedure name cannot be empty." });
            }
            var parameters = new List<StoredProcedureParameter>();

            try
            {
                using (IDbConnection connection = dbConnectionFactory.CreateConnection())
                {
                    // Cast to SqlConnection to use OpenAsync()
                    SqlConnection sqlConnection = connection as SqlConnection;
                    if (sqlConnection == null)
                    {
                        return StatusCode(500, new { success = false, message = "Could not cast IDbConnection to SqlConnection." });
                    }

                    await sqlConnection.OpenAsync(); // <--- Corrected: Use sqlConnection for async

                    string sqlQuery = @"
                    SELECT
                        p.name AS ParameterName,
                        TYPE_NAME(p.system_type_id) AS DataType,
                        p.has_default_value AS HasDefaultValue,
                        p.default_value AS DefaultValue
                    FROM
                        sys.procedures sp
                    JOIN
                        sys.parameters p ON sp.object_id = p.object_id
                    WHERE
                        sp.name = @procedureName
                        AND sp.schema_id = SCHEMA_ID('dbo'); -- Assuming 'dbo' schema
                ";

                    using (IDbCommand command = connection.CreateCommand()) // Keep as IDbCommand here
                    {
                        command.CommandText = sqlQuery;

                        var param = command.CreateParameter();
                        param.ParameterName = "@procedureName";
                        param.Value = procedureName;
                        command.Parameters.Add(param);

                        // Cast to SqlCommand to use ExecuteReaderAsync()
                        SqlCommand sqlCommand = command as SqlCommand;
                        if (sqlCommand == null)
                        {
                            return StatusCode(500, new { success = false, message = "Could not cast IDbCommand to SqlCommand." });
                        }

                        using (var reader = await sqlCommand.ExecuteReaderAsync()) // <--- Corrected: Use sqlCommand for async
                        {
                            while (await reader.ReadAsync())
                            {
                                parameters.Add(new StoredProcedureParameter
                                {
                                    ParameterName = reader["ParameterName"].ToString(),
                                    DataType = reader["DataType"].ToString(),
                                    HasDefaultValue = (bool)reader["HasDefaultValue"],
                                    DefaultValue = reader["DefaultValue"] == DBNull.Value ? null : reader["DefaultValue"].ToString()
                                });
                            }
                        }
                    }
                }
                return Ok(new { success = true, parameters });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching SP parameters: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Failed to load parameters: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveDataSource([FromBody] JsonElement payload)
        {
            if (!payload.TryGetProperty("FormId", out var formIdElement))
                return BadRequest("Missing 'formId'");

            int formId;
            if (formIdElement.ValueKind == JsonValueKind.Number)
                formId = formIdElement.GetInt32();
            else if (formIdElement.ValueKind == JsonValueKind.String && int.TryParse(formIdElement.GetString(), out var parsedFormId))
                formId = parsedFormId;
            else
                return BadRequest("Invalid formId");

            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
        await AppAuthorizeHelper.ValidateReadPermissionAsync(
            HttpContext,
            
            formAccessService,
            menuService,
            userSecurityStampCacheService, // <-- new arg
            formId,
            EnumContextFormType.PARENT        // <-- new arg
        );

            if (!isValid || serviceInterface == null)
                return Unauthorized("Access denied");

            try
            {
                var invoker = new ServiceInvoker(serviceProvider);

                // Dynamically resolve ViewModel type
                var modelType = invoker.GetViewModelType(projectNamespace, viewModel);

                // Deserialize the payload into the ViewModel type
                var model = JsonSerializer.Deserialize(payload.GetRawText(), modelType);

                // If null, something went wrong
                if (model == null)
                    return BadRequest("Model could not be deserialized.");

               

                // Try to invoke AddAsync if it exists
                await invoker.InvokeAsync(projectNamespace, serviceInterface, "AddAsync", model, formContext);

                return Ok(new { success = true, message = "Data source saved successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> ExecuteStoredProcedure([FromBody] List<StoredProcedureParameter> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return BadRequest(new { success = false, message = "No parameters provided." });

            string procedureName = parameters.First().ProcedureName;

            if (string.IsNullOrWhiteSpace(procedureName))
                return BadRequest(new { success = false, message = "Procedure name is required." });

            try
            {
                using var connection = dbConnectionFactory.CreateConnection();
                using var command = connection.CreateCommand();
                command.CommandText = procedureName;
                command.CommandType = CommandType.StoredProcedure;

                foreach (var param in parameters)
                {
                    var sqlParam = command.CreateParameter();
                    sqlParam.ParameterName = param.ParameterName;
                    sqlParam.Value = string.IsNullOrWhiteSpace(param.InputValue) ? DBNull.Value : param.InputValue;
                    command.Parameters.Add(sqlParam);
                }

                await ((SqlConnection)connection).OpenAsync();
                using var reader = await ((SqlCommand)command).ExecuteReaderAsync();

                var result = new List<Dictionary<string, object>>();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader[i];
                    }
                    result.Add(row);
                }

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetAllStoredProcedures()
        {
            var procedures = new List<string>();

            try
            {
                using (IDbConnection connection = dbConnectionFactory.CreateConnection())
                {
                    SqlConnection sqlConnection = connection as SqlConnection;
                    if (sqlConnection == null)
                        return StatusCode(500, "Connection casting failed.");

                    await sqlConnection.OpenAsync();

                    string sql = @"SELECT name FROM sys.procedures WHERE schema_id = SCHEMA_ID('dbo') ORDER BY name";

                    using (var cmd = new SqlCommand(sql, sqlConnection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            procedures.Add(reader["name"].ToString());
                        }
                    }
                }

                return Ok(new { success = true, procedures });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


    }
}


public class StoredProcedureParameter
{
    public string ProcedureName { get; set; }
    public string ParameterName { get; set; }
    public string DataType { get; set; }
    public bool HasDefaultValue { get; set; }
    public string? DefaultValue { get; set; }
    public string? InputValue { get; set; }
}



