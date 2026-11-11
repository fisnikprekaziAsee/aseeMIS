using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.IdentityMng.Controllers
{
    [Area("IdentityMng")]
    public class BatchGridController : Controller
    {
        private static List<GridData> _data = new List<GridData>
        {
            new GridData { Id = 1, Name = "Item 1", Quantity = 10, IsActive = false },
            new GridData { Id = 2, Name = "Item 2", Quantity = 20, IsActive = true },
            new GridData { Id = 3, Name = "Item 3", Quantity = 15, IsActive = false }
        };

        public IActionResult Index()
        {
            return View("~/Areas/IdentityMng/Views/Index.cshtml", _data);
        }

        [HttpPost]
        public IActionResult BatchSave([FromBody] BatchRequest batchRequest)
        {
            if (batchRequest.Changed != null)
            {
                foreach (var item in batchRequest.Changed)
                {
                    var existing = _data.FirstOrDefault(x => x.Id == item.Id);
                    if (existing != null)
                    {
                        existing.IsActive = item.IsActive;
                    }
                }
            }
            return Json(new { result = _data, count = _data.Count });
        }
    }

    public class GridData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public bool IsActive { get; set; } // Checkbox Column
    }

    public class BatchRequest
    {
        public List<GridData> Changed { get; set; }
    }
}