
using dsMIS.Core.Enums;

namespace aseeMIS.Areas.Common.ViewModels
{
    public sealed class GridWidgetViewModel 
    {

        // identity / naming
        public int FormId { get; init; }
        public string FormSessionId { get; init; }
        public int? Form360Id { get; init; }
        public EnumContextFormType ContextFormType { get; init; } = EnumContextFormType.PARENT;
        public string GridName { get; init; } = "";   // precomputed: "Grid_{FormId}_{TypeName}"
        public Type ModelType { get; init; } = typeof(object);

        // binding

        public object? Data { get; init; }            // used in local mode (and first-page prefetch when remote)
        public bool RemoteMode { get; init; }         // DataManager mode
        public string? RemoteSelectMethod { get; init; } // optional override

        // Remote-mode binding. RemoteUrl is the absolute path the DataManager hits
        // (e.g. "/api/griddata/paged"). TotalCount + PageSize describe the prefetched first
        // page so the front-end can serve it synchronously without a round-trip.
        public string? RemoteUrl { get; init; }
        public int? TotalCount { get; init; }
        public int? PageSize { get; init; }

        // fk+cascade
        public Dictionary<string, object>? ForeignKeyData { get; init; }
        public Dictionary<string, string>? ForeignKeyDataSourceMap { get; init; }
        public IEnumerable<object>? CascadeMap { get; init; }

        // misc
        public string? Area { get; init; }
        // identity / naming

        // Optional partial-dialog editor hook.
        // Empty means old behavior: Syncfusion inline/dialog editor and generic gridHelperGeneric.js save flow.
        public string? EditorPartialViewPath { get; init; }
        public string? EditorPartialAddPath { get; init; }
        public string? EditorPartialEditPath { get; init; }
        public string? EditorDialogTitle { get; init; }
        public string? EditorDialogDescription { get; init; }
        public string? EditorDialogLayoutJson { get; init; }
        public string? PreferredEditMode { get; init; }
        public string? ParentForeignAttributeName { get; init; }
        public string? ParentTargetId { get; init; }
        public string? ParentTargetText { get; init; }
        public bool HasColumnIndexDefaultVisibility { get; init; }
        public HashSet<int> ColumnIndexDefaultVisibility { get; init; } = [];
       
        // ui
        public bool IsGridReadOnly { get; init; } = false;   

        public bool? AllowGrouping { get; init; }
        public int? FrozenColumns { get; init; }
        public int? FrozenRows { get; init; }

        public bool FreezeApproveColumn { get; init; }
        public IEnumerable<string>? GroupBy { get; init; }
        public IEnumerable<string>? AgregateFunctionSumFields { get; init; }
        public IEnumerable<string>? AgregateFunctionMinFields { get; init; }
        public IEnumerable<string>? AgregateFunctionMaxFields { get; init; }
        public IEnumerable<string>? AgregateFunctionCountFields { get; init; }
        public IEnumerable<string>? AgregateFunctionAvgFields { get; init; }

        public object? ToolbarItems { get; init; }
        public object? ContextMenuItems { get; init; }

    }
}
