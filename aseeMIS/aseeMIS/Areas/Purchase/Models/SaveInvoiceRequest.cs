using dsPurchase.Core.ViewModels;

namespace aseeMIS.Areas.Purchase.Models
{
    public class SaveInvoiceRequest
    {
        public InvoiceAiViewModel Invoice { get; set; }
        public string RawJson { get; set; }
        public string OcrText { get; set; }
    }
}
