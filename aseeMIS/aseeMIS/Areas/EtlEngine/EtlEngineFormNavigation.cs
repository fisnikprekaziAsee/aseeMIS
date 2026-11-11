using dsMIS.Core.DTOs;
using dsMIS.Core.Services.Interfaces;

namespace aseeMIS.Areas.EtlEngine;

internal static class EtlEngineFormNavigation
{
    public static IReadOnlyList<int> ParseChildFormIds(AppFormMenuDto? form)
    {
        if (form == null || string.IsNullOrWhiteSpace(form.FlowFormChildIds))
        {
            return Array.Empty<int>();
        }

        return form.FlowFormChildIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    public static async Task<(int? FormId, string Href)> ResolveJobWorkflowFormAsync(
        IAppFormMenuService menuService,
        AppFormMenuDto? importForm,
        Func<int, string> fallbackHrefFactory)
    {
        var menus = await menuService.SelectAllAsync();
        int? workflowFormId = null;

        foreach (var childId in ParseChildFormIds(importForm))
        {
            var childMenu = menus.FirstOrDefault(m => m.FormId == childId);
            if (childMenu != null &&
                (childMenu.FormHref ?? string.Empty).Contains("EtlEngineJobWorkflow", StringComparison.OrdinalIgnoreCase))
            {
                workflowFormId = childId;
                break;
            }
        }

        if (workflowFormId == null)
        {
            workflowFormId = menus
                .FirstOrDefault(m =>
                    (m.FormHref ?? string.Empty).Contains("EtlEngineJobWorkflow", StringComparison.OrdinalIgnoreCase))
                ?.FormId;
        }

        if (workflowFormId == null)
        {
            workflowFormId = ParseChildFormIds(importForm).FirstOrDefault();
            if (workflowFormId == 0)
            {
                workflowFormId = null;
            }
        }

        if (workflowFormId == null || workflowFormId <= 0)
        {
            return (null, string.Empty);
        }

        var workflowMenu = menus.FirstOrDefault(m => m.FormId == workflowFormId);
        var href = !string.IsNullOrWhiteSpace(workflowMenu?.FormHref)
            ? workflowMenu!.FormHref!
            : fallbackHrefFactory(workflowFormId.Value);

        return (workflowFormId, href);
    }

    public static async Task<string> ResolveImportFormHrefAsync(
        IAppFormMenuService menuService,
        int importFormId,
        Func<int, string> fallbackHrefFactory)
    {
        var (_, href) = await ResolveImportFormAsync(menuService, importFormId, fallbackHrefFactory);
        return href;
    }

    /// <summary>Resolves the Import form (9901) when the current form is Import or Job Workflow (9902).</summary>
    public static async Task<(int FormId, string Href)> ResolveImportFormAsync(
        IAppFormMenuService menuService,
        int currentFormId,
        Func<int, string> fallbackHrefFactory)
    {
        var menus = await menuService.SelectAllAsync();
        var current = menus.FirstOrDefault(m => m.FormId == currentFormId);

        static bool IsImportHref(string? href) =>
            !string.IsNullOrWhiteSpace(href)
            && href.Contains("EtlEngine/EtlEngine", StringComparison.OrdinalIgnoreCase)
            && !href.Contains("JobWorkflow", StringComparison.OrdinalIgnoreCase);

        if (current != null && IsImportHref(current.FormHref))
        {
            var href = current.FormHref ?? fallbackHrefFactory(currentFormId);
            return (currentFormId, href);
        }

        if (current?.FormParentId is > 0)
        {
            var importMenu = menus.FirstOrDefault(m =>
                m.FormParentId == current.FormParentId && IsImportHref(m.FormHref));
            if (importMenu != null)
            {
                return (importMenu.FormId, importMenu.FormHref ?? fallbackHrefFactory(importMenu.FormId));
            }
        }

        var fallbackImport = menus.FirstOrDefault(m => IsImportHref(m.FormHref));
        if (fallbackImport != null)
        {
            return (fallbackImport.FormId, fallbackImport.FormHref ?? fallbackHrefFactory(fallbackImport.FormId));
        }

        return (currentFormId, fallbackHrefFactory(currentFormId));
    }
}
