using dsDwReport.Core.ViewModels;

namespace aseeMIS.Areas.DwReport.ViewModels
{
    public class DwReportPageViewModel
    {
        public List<DwReportTypeViewModel> ReportTypes { get; set; } = new();
        public List<DwReportViewModel> Reports { get; set; } = new();
    }
}
