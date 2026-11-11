using dsDwJob.Core.ViewModels;

namespace aseeMIS.Areas.DwJob.ViewModels
{
    public class DwJobPageViewModel
    {
        public List<DwJobViewModel> Jobs { get; set; } = new();

        public List<DwJobLogViewModel> JobLogs { get; set; }
    }
}
