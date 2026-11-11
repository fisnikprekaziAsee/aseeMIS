// Areas/Common/DTOs/EntityDetailRequest.cs
using dsMIS.Core.Enums;

namespace aseeMIS.Areas.Common.DTOs
{
    public class EntityDetailRequest
    {
        public string ViewPath { get; set; } = string.Empty; // required
        public object? Model { get; set; }                   // optional, your typed VM
        public string FormSessionId { get; set; } = string.Empty; // needed for Approve/Save headers
        public int FormId { get; set; }
        public string? EntityPrimaryKeyValue { get; set; }

        public string Area { get; set; } = "";
        public string IsApprovedText { get; set; } = "";

        public EnumEntityDetailHost Source { get; set; } = EnumEntityDetailHost.Entity360;
        public string? GridName { get; set; }
        public string? GridEditorAction { get; set; }
        public bool IsAdd { get; set; }
        public string? ParentForeignAttributeName { get; set; }
        public string? ParentTargetId { get; set; }
        public string? ParentTargetText { get; set; }

        public bool IsGridDialog => Source == EnumEntityDetailHost.Grid;
    }
}
