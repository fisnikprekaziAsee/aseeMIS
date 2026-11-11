using dsAppMng.Core.DTOs;

namespace aseeMIS.Areas.Common.ViewModels
{
    public class Entity360PageViewModel
    {
        public string FormSessionId { get; set; }
        public int FormId { get; set; }
        public string? ForeignAttribute { get; set; }
        public string? PrimaryKeyAttribute { get; set; }
        public string? PrimaryKey { get; set; }
        public string? HeaderPath { get; set; }
        public string? DetailPath { get; set; }
        public List<AppFormEntityChildDto>? ChildForms { get; set; }
        public string Area { get; set; } = "";
        public object? EntityModelData { get; set; }
    }
}
