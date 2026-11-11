using dsCreditGL.Core.ViewModels;
namespace aseeMIS.Areas.CreditGL.ViewModels
{
        public class CreditGLJournalPageViewModel
        {
            public List<CreditGLJournalHeaderViewModel> Headers { get; set; } = new();
            public List<CreditGLJournalLineViewModel> Lines { get; set; } = new();
        }
 
}
