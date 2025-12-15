using FitnessCenter.Data;
using FitnessCenter.Models;
using FitnessCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

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

        public async Task<IActionResult> Index(string statusFilter, string dateFilter, string searchTerm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            // Build LINQ query with multiple filters
            var query = _context.Appointments
                .Include(a => a.Member)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .AsQueryable();

            // Apply role-based filtering using LINQ
            if (!isAdmin)
            {
                query = query.Where(a => a.MemberId == user.Id);
            }

            // Apply status filter using LINQ
            if (!string.IsNullOrWhiteSpace(statusFilter) && 
                Enum.TryParse<AppointmentStatus>(statusFilter, out var status))
            {
                query = query.Where(a => a.Status == status);
            }

            // Apply date filter using LINQ
            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                var today = DateTime.Today;
                query = dateFilter.ToLower() switch
                {
                    "today" => query.Where(a => a.AppointmentDateTime.Date == today),
                    "week" => query.Where(a => a.AppointmentDateTime >= today && 
                                              a.AppointmentDateTime <= today.AddDays(7)),
                    "month" => query.Where(a => a.AppointmentDateTime >= today && 
                                               a.AppointmentDateTime <= today.AddMonths(1)),
                    "past" => query.Where(a => a.AppointmentDateTime < today),
                    "upcoming" => query.Where(a => a.AppointmentDateTime >= today),
                    _ => query
                };
            }

            // Apply search term using LINQ
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(a => 
                    (a.Member != null && (a.Member.FirstName.Contains(searchTerm) || 
                                         a.Member.LastName.Contains(searchTerm))) ||
                    (a.Trainer != null && (a.Trainer.FirstName.Contains(searchTerm) || 
                                          a.Trainer.LastName.Contains(searchTerm))) ||
                    (a.Service != null && a.Service.Name.Contains(searchTerm)) ||
                    (a.Notes != null && a.Notes.Contains(searchTerm)));
            }

            // Order by date descending using LINQ
            var appointments = await query
                .OrderByDescending(a => a.AppointmentDateTime)
                .ToListAsync();

            // Get statistics using LINQ aggregations
            var allAppointments = isAdmin
                ? await _context.Appointments.ToListAsync()
                : await _context.Appointments.Where(a => a.MemberId == user.Id).ToListAsync();

            ViewBag.Statistics = new
            {
                Total = allAppointments.Count,
                Pending = allAppointments.Count(a => a.Status == AppointmentStatus.Pending),
                Confirmed = allAppointments.Count(a => a.Status == AppointmentStatus.Confirmed),
                Completed = allAppointments.Count(a => a.Status == AppointmentStatus.Completed),
                Cancelled = allAppointments.Count(a => a.Status == AppointmentStatus.Cancelled),
                TotalRevenue = allAppointments
                    .Where(a => a.Status == AppointmentStatus.Completed)
                    .Sum(a => a.Price),
                UpcomingCount = allAppointments.Count(a => a.AppointmentDateTime >= DateTime.Now),
                // Group appointments by status using LINQ
                AppointmentsByStatus = allAppointments
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                    .ToList(),
                // Group appointments by month using LINQ
                AppointmentsByMonth = allAppointments
                    .Where(a => a.AppointmentDateTime >= DateTime.Now.AddMonths(-6))
                    .GroupBy(a => new { a.AppointmentDateTime.Year, a.AppointmentDateTime.Month })
                    .Select(g => new { 
                        Year = g.Key.Year, 
                        Month = g.Key.Month, 
                        Count = g.Count() 
                    })
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToList()
            };

            ViewBag.StatusFilter = statusFilter;
            ViewBag.DateFilter = dateFilter;
            ViewBag.SearchTerm = searchTerm;

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
            
            // Use LINQ Select to transform data
            var result = trainers
                .Select(t => new 
                { 
                    t.Id, 
                    t.FullName,
                    t.Email,
                    t.Phone,
                    t.Bio,
                    // Count upcoming appointments using LINQ
                    UpcomingAppointments = _context.Appointments
                        .Count(a => a.TrainerId == t.Id && 
                                   a.AppointmentDateTime >= DateTime.Now && 
                                   a.Status != AppointmentStatus.Cancelled)
                })
                .OrderBy(t => t.FullName)
                .ToList();
            
            return Json(result);
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

