
using dsCredinsEtl.Core.ViewModels;

namespace aseeMIS.Areas.DwEtlValidation.ViewModels
{
    public class DwEtlValidationPageViewModel
    {
        public List<DwEtlJobValidationTypeViewModel> Validations { get; set; } = new();
        public List<DwEtlJobValidationLogDetailViewModel> ValidationDetails { get; set; } = new();
        public List<DwEtlJobValidationLogViewModel> ValidationLog { get; set; } = new();

       
    }
}
