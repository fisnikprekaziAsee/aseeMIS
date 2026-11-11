using dsMIS.Application.Utils;
using dsMIS.Core.DTOs;
using dsMIS.Core.Enums;
using aseeMIS.Areas.Common.Utils.Parameters;
using aseeMIS.Utils;

namespace aseeMIS.Areas.Common.DTOs
{
    public class BuildContext
    {
        public AppFormMenuDto AppForm { get; init; } = default!;
        public ContextUserDto FormContext { get; init; } = default!;
        public string ProjectNs { get; init; } = "";
        public string ServiceInterface { get; init; } = "";
        public string ViewModelName { get; init; } = "";
        public EnumFormType FormTypeFromConfig { get; init; }
        public ServiceInvoker Invoker { get; init; } = default!;
        public string? MethodName { get; init; }
    }
}
