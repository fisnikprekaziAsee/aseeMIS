namespace aseeMIS.Areas.Common.DTOs
{
    public class GridUiConfig
    {
        public bool? AllowGrouping { get; init; }
        public string[]? GroupBy { get; init; }
        public int? FrozenColumns { get; init; }
        public int? FrozenRows { get; init; }

        public bool FreezeApproveColumn { get; init; }
    }
}
