using dsDef.Core.Services.Interfaces;
using dsDef.Core.Models; // for DefHolidayModel
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dsDef.Core.Data;
using Syncfusion.EJ2.Schedule;
using dsDef.Core.ViewModels;

namespace aseeMIS.Areas.Calendars.Controllers
{
    [Area("Calendars")]
    public class HolidayController : Controller
    {
        private readonly IDefHolidayService _defHolidayService;
        private readonly IDefCountryService _defCountryService; // Add your DBContext here for Countries

        public HolidayController(IDefHolidayService defHolidayService, IDefCountryService defCountryService)
        {
            _defHolidayService = defHolidayService;
            _defCountryService = defCountryService;
        }

        public async Task<IActionResult> Index()
        {
            // Load countries dropdown
            ViewBag.Countries = await _defCountryService.SelectAllAsync();

            // Load existing holidays as datasource for calendar
            var holidays = await _defHolidayService.SelectAllAsync();

            // Map holidays to Schedule Event model
            ViewBag.datasource = holidays.Select(h => new
            {
                Id = h.HolidayId,
                Subject = h.HolidayName,
                StartTime = h.HolidayDate,
                EndTime = h.HolidayDate,
                IsAllDay = true
            }).ToList();

            // Set view options for schedule
            ViewBag.view = new List<ScheduleView>
            {
                new ScheduleView { Option = Syncfusion.EJ2.Schedule.View.Agenda }, // Here! ✅
                new ScheduleView { Option = Syncfusion.EJ2.Schedule.View.Month },
                new ScheduleView { Option = Syncfusion.EJ2.Schedule.View.Year }
            };

            return View("~/Areas/Calendars/Views/Holiday.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> AddHoliday([FromBody] DefHolidayViewModel model, [FromQuery] string countryCode)
        {
            //await _defHolidayService.AddAsync(new List<DefHolidayViewModel> { model }, countryCode);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateHoliday([FromBody] DefHolidayViewModel model)
        {
            //await _defHolidayService.UpdateAsync(model);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHoliday([FromBody] int holidayId)
        {
            //await _defHolidayService(holidayId);
            return Ok();
        }
    }
}
