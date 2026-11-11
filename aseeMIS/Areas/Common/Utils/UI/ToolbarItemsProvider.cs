using DocumentFormat.OpenXml.Office.Word;
using dsMIS.Core.DTOs;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.Parameters;
using Syncfusion.DocIO.DLS;
using Syncfusion.EJ2.Navigations;

namespace aseeMIS.Areas.Common.Utils.UI
{
    public class ToolbarItemsProvider(ILocalizationService localizationService)
    {
        /// <summary>
        /// Builds Syncfusion Grid toolbar items according to form definition and user rights.
        /// NOTE: When contextSessionDto.IsModifiable is true, we add the "Open 360" button (id = "Open360").
        /// The client JS will navigate to /Common/Entity360/Index using data-form-config-id from the grid container.
        /// </summary>
        public object GetToolbarItems(AppFormMenuDto appForm, ContextSessionDto contextSessionDto, EnumFormType formType)
        {
            var items = new List<object>();
            bool save = false;

            if (formType == EnumFormType.CRUD)
            {
                if (appForm.IsInsertable && contextSessionDto.IsInsertable) { items.Add("Add"); save = true; }
                if (contextSessionDto.IsUpdatable) { items.Add("Edit"); save = true; }

                if (contextSessionDto.IsModifiable)
                    items.Add(new
                    {
                        tooltipText = "360",
                        prefixIcon = "bi bi-pencil-square",
                        id = "Open360"
                    });

                if (contextSessionDto.IsDeletable) items.Add("Delete");

                if (contextSessionDto.IsApprovable)
                    items.Add(new { tooltipText = "ApproveForm", prefixIcon = "bi-check2-circle", id = "ApproveForm", align = "Right" });

                if (contextSessionDto.IsPublishable)
                    items.Add(new { tooltipText = "Publish", prefixIcon = "bi bi-upload", id = "Publish", align = "Right" });

            }
            else if (formType == EnumFormType.TRANSLATOR)
            {
                if (contextSessionDto.IsUpdatable) { items.Add("Edit"); save = true; }
                if (contextSessionDto.IsApprovable)
                    items.Add(new { tooltipText = "ApproveForm", prefixIcon = "bi-check2-circle", id = "ApproveForm", align = "Right" });
                if (contextSessionDto.IsPublishable)
                    items.Add(new { tooltipText = "Publish", prefixIcon = "bi bi-upload", id = "Publish", align = "Right" });

                //items.Add(new { tooltipText = "Filter", prefixIcon = "bi bi-sliders", id = "ToggleFilter", align = "Right" });
            }
            else if (formType == EnumFormType.SDLT)
            {
                if (contextSessionDto.IsApprovable)
                    items.Add(new { tooltipText = "ApproveForm", prefixIcon = "bi-check2-circle", id = "ApproveForm", align = "Right" });

                // items.Add(new { tooltipText = "Filter", prefixIcon = "bi bi-sliders", id = "ToggleFilter", align = "Right" });
            }
            else if (formType == EnumFormType.READONLY)
            {
                // Pure read-only grid (history/log). No Add/Edit/Delete/Approve/Publish.
                // Only the always-on Search + ColumnChooser + optional Excel/Pdf export below.
            }


            if (contextSessionDto.IsExportable) items.Add("ExcelExport");
            if (contextSessionDto.IsPrintable) items.Add("PdfExport");

            if (save) { items.Add("Update"); items.Add("Cancel"); }

            //items.Add("Search");

            //items.Add(new
            //{
            //    prefixIcon = "e-expand",
            //    id = "Grid_Mode",
            //    align = "Right"
            //});
            //items.Add("ColumnChooser");

            // Your always-on defaults
            items.Add(new
            {
                tooltipText = "Refresh",
                prefixIcon = "e-icons e-refresh",
                id = "RefreshGrid",
                align = "Right"
            });
            items.Add("Search");
            items.Add("ColumnChooser");

            

            return items;
        }

        public async Task<object> GetContextMenuItems(ContextSessionDto contextSessionDto)
        {
            var contextMenuItems = new List<object>
            {
                "AutoFit", "AutoFitAll", "SortAscending", "SortDescending", "Copy",
                new { text = await localizationService.FindButtonTextByKeyAndLanguageCodeAsync("CWH", contextSessionDto.LanguageCode), id = "copyWithHeader", iconCss = "e-icons e-copy" },
                new { text = "Freeze column", id = "FreezeColumn", target = ".e-headercell", iconCss = "e-icons e-freeze" },
                new { text = "Unfreeze column", id = "UnfreezeColumn", target = ".e-headercell", iconCss = "e-icons e-freeze" }
            };


            if (contextSessionDto.IsExportable == true)
            {
                contextMenuItems.Add("ExcelExport");
                contextMenuItems.Add("CsvExport");
            }
            if (contextSessionDto.IsPrintable == true)
            {
                contextMenuItems.Add("PdfExport");
            }

            return contextMenuItems;
        }
    }
}
