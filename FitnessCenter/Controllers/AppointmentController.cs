using FitnessCenter.Data;
using FitnessCenter.Models;
using FitnessCenter.Services;
using FitnessCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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

            // Apply search term using LINQ (case-insensitive)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.ToLower();
                query = query.Where(a => 
                    (a.Member != null && 
                     ((a.Member!.FirstName != null && a.Member.FirstName.ToLower().Contains(searchLower)) || 
                      (a.Member!.LastName != null && a.Member.LastName.ToLower().Contains(searchLower)))) ||
                    (a.Trainer != null && 
                     (a.Trainer.FirstName.ToLower().Contains(searchLower) || 
                      a.Trainer.LastName.ToLower().Contains(searchLower))) ||
                    (a.Service != null && a.Service.Name.ToLower().Contains(searchLower)) ||
                    (a.Notes != null && a.Notes.ToLower().Contains(searchLower)));
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

        [Authorize(Roles = "Member,Admin")]
        public async Task<IActionResult> Create(string? memberId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            
            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name");
            
            // If admin, allow selecting a member
            if (isAdmin)
            {
                var members = await _context.Users
                    .Where(u => u.Email != null)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .Select(u => new { u.Id, FullName = u.FullName, u.Email })
                    .ToListAsync();
                
                ViewData["MemberId"] = new SelectList(members, "Id", "FullName", memberId);
                ViewBag.IsAdmin = true;
                ViewBag.SelectedMemberId = memberId;
            }
            else
            {
                ViewBag.IsAdmin = false;
            }
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Member,Admin")]
        public async Task<IActionResult> Create(CreateAppointmentViewModel model, string? selectedMemberId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            string targetMemberId;

            // Determine which member to use
            if (isAdmin && !string.IsNullOrEmpty(selectedMemberId))
            {
                // Admin creating for another user
                targetMemberId = selectedMemberId;
            }
            else
            {
                // Regular user or admin creating for themselves
                targetMemberId = user.Id;
            }

            if (ModelState.IsValid)
            {
                var service = await _context.Services.FindAsync(model.ServiceId);
                if (service == null)
                {
                    ModelState.AddModelError("ServiceId", "Service not found.");
                    ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", model.ServiceId);
                    if (isAdmin)
                    {
                        var members = await _context.Users
                            .Where(u => u.Email != null)
                            .OrderBy(u => u.FirstName)
                            .ThenBy(u => u.LastName)
                            .Select(u => new { u.Id, FullName = u.FullName, u.Email })
                            .ToListAsync();
                        ViewData["MemberId"] = new SelectList(members, "Id", "FullName", selectedMemberId);
                    }
                    return View(model);
                }

                // Check if trainer is available
                if (!await _appointmentService.IsTrainerAvailableAsync(
                    model.TrainerId, model.AppointmentDateTime, service.DurationMinutes))
                {
                    ModelState.AddModelError("", "The selected trainer is not available at this time.");
                    ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", model.ServiceId);
                    ViewData["TrainerId"] = new SelectList(await _appointmentService.GetAvailableTrainersAsync(model.AppointmentDateTime, model.ServiceId), "Id", "FullName", model.TrainerId);
                    if (isAdmin)
                    {
                        var members = await _context.Users
                            .Where(u => u.Email != null)
                            .OrderBy(u => u.FirstName)
                            .ThenBy(u => u.LastName)
                            .Select(u => new { u.Id, FullName = u.FullName, u.Email })
                            .ToListAsync();
                        ViewData["MemberId"] = new SelectList(members, "Id", "FullName", selectedMemberId);
                    }
                    return View(model);
                }

                var targetMember = await _context.Users.FindAsync(targetMemberId);
                if (targetMember == null)
                {
                    ModelState.AddModelError("", "Selected member not found.");
                    ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", model.ServiceId);
                    if (isAdmin)
                    {
                        var members = await _context.Users
                            .Where(u => u.Email != null)
                            .OrderBy(u => u.FirstName)
                            .ThenBy(u => u.LastName)
                            .Select(u => new { u.Id, FullName = u.FullName, u.Email })
                            .ToListAsync();
                        ViewData["MemberId"] = new SelectList(members, "Id", "FullName", selectedMemberId);
                    }
                    return View(model);
                }

                var appointment = new Appointment
                {
                    MemberId = targetMemberId,
                    TrainerId = model.TrainerId,
                    ServiceId = model.ServiceId,
                    AppointmentDateTime = model.AppointmentDateTime,
                    DurationMinutes = service.DurationMinutes,
                    Price = service.Price,
                    Status = isAdmin ? AppointmentStatus.Confirmed : AppointmentStatus.Pending, // Admin can auto-confirm
                    Notes = model.Notes,
                    CreatedDate = DateTime.Now
                };

                _context.Add(appointment);
                await _context.SaveChangesAsync();

                // Send confirmation email
                var trainer = await _context.Trainers.FindAsync(model.TrainerId);
                if (trainer != null && targetMember.Email != null)
                {
                    await _emailService.SendAppointmentConfirmationAsync(
                        targetMember.Email, targetMember.FullName, model.AppointmentDateTime, trainer.FullName);
                }

                TempData["SuccessMessage"] = isAdmin 
                    ? $"Appointment created successfully for {targetMember.FullName}." 
                    : "Appointment created successfully. Waiting for admin approval.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", model.ServiceId);
            if (model.ServiceId > 0)
            {
                ViewData["TrainerId"] = new SelectList(await _appointmentService.GetAvailableTrainersAsync(model.AppointmentDateTime, model.ServiceId), "Id", "FullName", model.TrainerId);
            }
            if (isAdmin)
            {
                var members = await _context.Users
                    .Where(u => u.Email != null)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .Select(u => new { u.Id, FullName = u.FullName, u.Email })
                    .ToListAsync();
                ViewData["MemberId"] = new SelectList(members, "Id", "FullName", selectedMemberId);
                ViewBag.IsAdmin = true;
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

        [HttpGet]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> GetAvailableTimeSlots(int trainerId, string date, int durationMinutes)
        {
            try
            {
                if (string.IsNullOrEmpty(date))
                {
                    _logger.LogWarning("Empty date parameter");
                    return Json(new List<object>());
                }

                // Try parsing the date - handle both "YYYY-MM-DD" and other formats
                DateTime parsedDate;
                if (!DateTime.TryParse(date, out parsedDate))
                {
                    // Try parsing as date-only format
                    if (!DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out parsedDate))
                    {
                        _logger.LogWarning("Invalid date parameter: {Date}", date);
                        return Json(new List<object>());
                    }
                }

                // Ensure we're working with just the date part (no time)
                parsedDate = parsedDate.Date;

                if (durationMinutes <= 0)
                {
                    _logger.LogWarning("Invalid duration: {Duration}", durationMinutes);
                    return Json(new List<object>());
                }

                _logger.LogInformation("Getting time slots for trainer {TrainerId}, date {Date} ({DayOfWeek}), duration {Duration}", 
                    trainerId, parsedDate, parsedDate.DayOfWeek, durationMinutes);

                // Check if trainer exists and has working hours
                var trainer = await _context.Trainers
                    .Include(t => t.WorkingHours)
                    .FirstOrDefaultAsync(t => t.Id == trainerId);

                if (trainer == null)
                {
                    _logger.LogWarning("Trainer {TrainerId} not found", trainerId);
                    return Json(new List<object>());
                }

                var workingHoursForDay = trainer.WorkingHours
                    .FirstOrDefault(wh => wh.DayOfWeek == parsedDate.DayOfWeek);

                if (workingHoursForDay == null)
                {
                    _logger.LogWarning("No working hours found for trainer {TrainerId} on {DayOfWeek}. Available days: {Days}", 
                        trainerId, parsedDate.DayOfWeek, 
                        string.Join(", ", trainer.WorkingHours.Select(wh => wh.DayOfWeek.ToString())));
                    return Json(new List<object>());
                }

                _logger.LogInformation("Found working hours: {StartTime} - {EndTime}", 
                    workingHoursForDay.StartTime, workingHoursForDay.EndTime);

                var timeSlots = await _appointmentService.GetAvailableTimeSlotsAsync(trainerId, parsedDate, durationMinutes);
                
                _logger.LogInformation("Found {Count} available time slots", timeSlots.Count);

                var result = timeSlots
                    .Select(ts => new
                    {
                        Time = ts.ToString(@"hh\:mm"),
                        DateTime = parsedDate.Date.Add(ts).ToString("yyyy-MM-ddTHH:mm")
                    })
                    .ToList();
                
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available time slots");
                return Json(new List<object>());
            }
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
            var appointment = await _context.Appointments
                .Include(a => a.Member)
                .FirstOrDefaultAsync(a => a.Id == id);
            
            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isOwner = appointment.MemberId == user.Id;

            // Only allow cancellation if user is owner or admin, and appointment is not completed
            if (!isAdmin && !isOwner)
            {
                TempData["ErrorMessage"] = "You don't have permission to cancel this appointment.";
                return RedirectToAction(nameof(Index));
            }

            if (appointment.Status == AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Cannot cancel a completed appointment.";
                return RedirectToAction(nameof(Index));
            }

            // Cancel instead of delete (soft delete by changing status)
            appointment.Status = AppointmentStatus.Cancelled;
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Appointment cancelled successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction(nameof(Index));
            }

            if (appointment.Status == AppointmentStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Cannot approve a cancelled appointment.";
                return RedirectToAction(nameof(Index));
            }

            appointment.Status = AppointmentStatus.Confirmed;
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Appointment approved successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Complete(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction(nameof(Index));
            }

            appointment.Status = AppointmentStatus.Completed;
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Appointment marked as completed.";
            return RedirectToAction(nameof(Index));
        }
    }
}

