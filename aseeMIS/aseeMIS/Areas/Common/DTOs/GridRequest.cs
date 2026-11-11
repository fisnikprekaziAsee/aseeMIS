using dsMIS.Core.Enums;

namespace aseeMIS.Areas.Common.DTOs
{
    // Request DTO you can pass from pages
    public class GridRequest
    {
        public int FormId { get; set; }
        public string? FormSessionId { get; set; }
        public string? ForeignAttributeName { get; set; }

        public string? PrimaryAttributeName { get; set; }
        public string? TargetId { get; set; }
        public string? TargetText { get; set; }

        public EnumContextFormType ContextFormType { get; set; } = EnumContextFormType.PARENT;
        public EnumFormType FormType { get; set; }
        public string GridType { get; set; } = "CRUD"; // "CRUD" | "FILE" | "REPORT"
        public bool RemoteMode { get; set; } = false;
        public string? OverrideMethodName { get; set; } // optional backend method override
        public string? GridName { get; set; }           // optional custom id

        // Optional partial-dialog editor for detail grids that need a richer form than Syncfusion inline/dialog editing.
        // When this is set, the grid still uses generic CRUD/data loading, but Add/Edit opens the supplied partial view.
        public string? EditorPartialViewPath { get; set; }
        public string? EditorDialogTitle { get; set; }

        // Optional visual layout for the built-in Syncfusion dialog editor.
        // Controls/save remain generic; this only lets gridHelperGeneric.js arrange fields into sections/cards.
        public string? PreferredEditMode { get; set; }
    }
}
