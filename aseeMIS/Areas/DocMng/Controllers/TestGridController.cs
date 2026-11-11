using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.DocMng.Controllers
{
    [Area("DocMng")]
    public class TestGridController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Areas/DocMng/Views/TestLoad.cshtml");
        }

        public IActionResult GridPartial()
        {
            var data = new List<SimpleOrder>
            {
                new() { OrderID = 10248, CustomerID = "VINET", OrderDate = DateTime.Now.AddDays(-2), Freight = 32.38m },
                new() { OrderID = 10249, CustomerID = "TOMSP", OrderDate = DateTime.Now.AddDays(-1), Freight = 11.61m },
                new() { OrderID = 10250, CustomerID = "HANAR", OrderDate = DateTime.Now, Freight = 65.83m }
            };

            return PartialView("~/Areas/DocMng/Views/_TestGridPartial.cshtml", data);
        }
    }

    public class SimpleOrder
    {
        public int OrderID { get; set; }
        public string CustomerID { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal Freight { get; set; }
    }
}
