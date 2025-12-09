using FitnessCenter.Data;
using FitnessCenter.Models;
using FitnessCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Controllers
{
    [Authorize]
    public class AppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Member> _userManager;
        private readonly IAppointmentService _appointmentService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AppointmentController> _logger;

        public AppointmentController(
            ApplicationDbContext context,
            UserManager<Member> userManager,
            IAppointmentService appointmentService,
            IEmailService emailService,
            ILogger<AppointmentController> logger)
        {
            _context = context;
            _userManager = userManager;
            _appointmentService = appointmentService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            var appointments = isAdmin
                ? await _context.Appointments
                    .Include(a => a.Member)
                    .Include(a => a.Trainer)
                    .Include(a => a.Service)
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .ToListAsync()
                : await _context.Appointments
                    .Where(a => a.MemberId == user.Id)
                    .Include(a => a.Trainer)
                    .Include(a => a.Service)
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .ToListAsync();

            return View(appointments);
        }

        [Authorize(Roles = "Member")]
        public IActionResult Create()
        {
            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Create(CreateAppointmentViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                var service = await _context.Services.FindAsync(model.ServiceId);
                if (service == null)
                {
                    ModelState.AddModelError("ServiceId", "Service not found.");
                    ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", model.ServiceId);
                    return View(model);
                }

                // Check if trainer is available
                if (!await _appointmentService.IsTrainerAvailableAsync(
                    model.TrainerId, model.AppointmentDateTime, service.DurationMinutes))
                {
                    ModelState.AddModelError("", "The selected trainer is not available at this time.");
                    ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", model.ServiceId);
                    ViewData["TrainerId"] = new SelectList(await _appointmentService.GetAvailableTrainersAsync(model.AppointmentDateTime, model.ServiceId), "Id", "FullName", model.TrainerId);
                    return View(model);
                }

                var appointment = new Appointment
                {
                    MemberId = user.Id,
                    TrainerId = model.TrainerId,
                    ServiceId = model.ServiceId,
                    AppointmentDateTime = model.AppointmentDateTime,
                    DurationMinutes = service.DurationMinutes,
                    Price = service.Price,
                    Status = AppointmentStatus.Pending,
                    Notes = model.Notes,
                    CreatedDate = DateTime.Now
                };

                _context.Add(appointment);
                await _context.SaveChangesAsync();

                // Send confirmation email
                var trainer = await _context.Trainers.FindAsync(model.TrainerId);
                if (trainer != null)
                {
                    await _emailService.SendAppointmentConfirmationAsync(
                        user.Email!, user.FullName, model.AppointmentDateTime, trainer.FullName);
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", model.ServiceId);
            if (model.ServiceId > 0)
            {
                ViewData["TrainerId"] = new SelectList(await _appointmentService.GetAvailableTrainersAsync(model.AppointmentDateTime, model.ServiceId), "Id", "FullName", model.TrainerId);
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> GetAvailableTrainers(int serviceId, DateTime appointmentDateTime)
        {
            var trainers = await _appointmentService.GetAvailableTrainersAsync(appointmentDateTime, serviceId);
            return Json(trainers.Select(t => new { t.Id, t.FullName }));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Member)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, AppointmentStatus status)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            appointment.Status = status;
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }

    public class CreateAppointmentViewModel
    {
        [Required]
        [Display(Name = "Service")]
        public int ServiceId { get; set; }

        [Required]
        [Display(Name = "Trainer")]
        public int TrainerId { get; set; }

        [Required]
        [Display(Name = "Appointment Date & Time")]
        [DataType(DataType.DateTime)]
        public DateTime AppointmentDateTime { get; set; }

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }
    }
}

