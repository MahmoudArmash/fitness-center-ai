using FitnessCenter.Data;
using FitnessCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FitnessCenter.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Use LINQ aggregations for statistics
            var appointments = await _context.Appointments.ToListAsync();
            var services = await _context.Services.ToListAsync();
            var trainers = await _context.Trainers.ToListAsync();
            var members = await _context.Users.ToListAsync();

            // Basic counts using LINQ
            ViewBag.TotalMembers = members.Count();
            ViewBag.TotalTrainers = trainers.Count();
            ViewBag.TotalAppointments = appointments.Count();
            ViewBag.PendingAppointments = appointments.Count(a => a.Status == AppointmentStatus.Pending);
            ViewBag.TotalServices = services.Count();

            // Advanced LINQ aggregations
            ViewBag.Statistics = new
            {
                // Appointment statistics using LINQ
                AppointmentStats = new
                {
                    Completed = appointments.Count(a => a.Status == AppointmentStatus.Completed),
                    Confirmed = appointments.Count(a => a.Status == AppointmentStatus.Confirmed),
                    Cancelled = appointments.Count(a => a.Status == AppointmentStatus.Cancelled),
                    TotalRevenue = appointments
                        .Where(a => a.Status == AppointmentStatus.Completed)
                        .Sum(a => a.Price),
                    AverageAppointmentPrice = appointments.Any() 
                        ? appointments.Average(a => a.Price) 
                        : 0,
                    AppointmentsByMonth = appointments
                        .GroupBy(a => new { a.AppointmentDateTime.Year, a.AppointmentDateTime.Month })
                        .Select(g => new { 
                            Year = g.Key.Year, 
                            Month = g.Key.Month, 
                            Count = g.Count(),
                            Revenue = g.Where(a => a.Status == AppointmentStatus.Completed).Sum(a => a.Price)
                        })
                        .OrderByDescending(x => x.Year)
                        .ThenByDescending(x => x.Month)
                        .Take(6)
                        .ToList()
                },
                // Service statistics using LINQ
                ServiceStats = new
                {
                    AveragePrice = services.Any() ? services.Average(s => s.Price) : 0,
                    ServicesByType = services
                        .GroupBy(s => s.Type)
                        .Select(g => new { Type = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList(),
                    MostPopularService = appointments
                        .GroupBy(a => a.ServiceId)
                        .Select(g => new { 
                            ServiceId = g.Key, 
                            Count = g.Count(),
                            ServiceName = services.FirstOrDefault(s => s.Id == g.Key)?.Name ?? "Unknown"
                        })
                        .OrderByDescending(x => x.Count)
                        .FirstOrDefault()
                },
                // Trainer statistics using LINQ
                TrainerStats = new
                {
                    TrainersByFitnessCenter = trainers
                        .GroupBy(t => t.FitnessCenterId)
                        .Select(g => new { 
                            FitnessCenterId = g.Key, 
                            Count = g.Count() 
                        })
                        .ToList(),
                    MostBookedTrainer = appointments
                        .GroupBy(a => a.TrainerId)
                        .Select(g => new { 
                            TrainerId = g.Key, 
                            Count = g.Count(),
                            TrainerName = trainers.FirstOrDefault(t => t.Id == g.Key)?.FullName ?? "Unknown"
                        })
                        .OrderByDescending(x => x.Count)
                        .FirstOrDefault()
                },
                // Member statistics using LINQ
                MemberStats = new
                {
                    MembersWithAppointments = members
                        .Where(m => appointments.Any(a => a.MemberId == m.Id))
                        .Count(),
                    TopMembers = appointments
                        .GroupBy(a => a.MemberId)
                        .Select(g => new { 
                            MemberId = g.Key, 
                            AppointmentCount = g.Count(),
                            TotalSpent = g.Where(a => a.Status == AppointmentStatus.Completed).Sum(a => a.Price),
                            MemberName = members.FirstOrDefault(m => m.Id == g.Key)?.FullName ?? "Unknown"
                        })
                        .OrderByDescending(x => x.AppointmentCount)
                        .Take(5)
                        .ToList()
                }
            };

            return View();
        }

        #region FitnessCenter CRUD
        public async Task<IActionResult> FitnessCenters()
        {
            return View(await _context.FitnessCenters.ToListAsync());
        }

        public IActionResult CreateFitnessCenter()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFitnessCenter(Models.FitnessCenter fitnessCenter)
        {
            if (ModelState.IsValid)
            {
                fitnessCenter.CreatedDate = DateTime.Now;
                _context.Add(fitnessCenter);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(FitnessCenters));
            }
            return View(fitnessCenter);
        }

        public async Task<IActionResult> EditFitnessCenter(int? id)
        {
            if (id == null) return NotFound();
            var fitnessCenter = await _context.FitnessCenters.FindAsync(id);
            if (fitnessCenter == null) return NotFound();
            return View(fitnessCenter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFitnessCenter(int id, Models.FitnessCenter fitnessCenter)
        {
            if (id != fitnessCenter.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(fitnessCenter);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FitnessCenterExists(fitnessCenter.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(FitnessCenters));
            }
            return View(fitnessCenter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFitnessCenter(int id)
        {
            var fitnessCenter = await _context.FitnessCenters.FindAsync(id);
            if (fitnessCenter != null)
            {
                _context.FitnessCenters.Remove(fitnessCenter);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(FitnessCenters));
        }

        private bool FitnessCenterExists(int id) => _context.FitnessCenters.Any(e => e.Id == id);
        #endregion

        #region Service CRUD
        public async Task<IActionResult> Services(string searchTerm, string serviceType, int? fitnessCenterId)
        {
            var query = _context.Services.Include(s => s.FitnessCenter).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.ToLower();
                query = query.Where(s => 
                    s.Name.ToLower().Contains(searchLower) || 
                    (s.Description != null && s.Description.ToLower().Contains(searchLower)));
            }

            if (!string.IsNullOrWhiteSpace(serviceType) &&
                Enum.TryParse<ServiceType>(serviceType, out var parsedServiceType))
            {
                query = query.Where(s => s.Type == parsedServiceType);
            }

            if (fitnessCenterId.HasValue)
            {
                query = query.Where(s => s.FitnessCenterId == fitnessCenterId.Value);
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.ServiceType = serviceType;
            ViewBag.FitnessCenterId = fitnessCenterId;
            ViewBag.FitnessCenters = new SelectList(await _context.FitnessCenters.OrderBy(fc => fc.Name).ToListAsync(), "Id", "Name", fitnessCenterId);
            ViewBag.ServiceTypes = Enum.GetValues(typeof(ServiceType)).Cast<ServiceType>();

            return View(await query.OrderBy(s => s.Name).ToListAsync());
        }

        public IActionResult CreateService()
        {
            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(Service service)
        {
            if (ModelState.IsValid)
            {
                _context.Add(service);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Services));
            }
            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name", service.FitnessCenterId);
            return View(service);
        }

        public async Task<IActionResult> EditService(int? id)
        {
            if (id == null) return NotFound();
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();
            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name", service.FitnessCenterId);
            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditService(int id, Service service)
        {
            if (id != service.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(service);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServiceExists(service.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Services));
            }
            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name", service.FitnessCenterId);
            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteService(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                _context.Services.Remove(service);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Services));
        }

        private bool ServiceExists(int id) => _context.Services.Any(e => e.Id == id);
        #endregion

        #region Trainer CRUD
        public async Task<IActionResult> Trainers(string searchTerm, int? fitnessCenterId)
        {
            var query = _context.Trainers.Include(t => t.FitnessCenter).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.ToLower();
                query = query.Where(t => 
                    t.FirstName.ToLower().Contains(searchLower) || 
                    t.LastName.ToLower().Contains(searchLower) ||
                    (t.Email != null && t.Email.ToLower().Contains(searchLower)) ||
                    (t.Phone != null && t.Phone.Contains(searchTerm))); // Phone numbers usually don't need case-insensitive
            }

            if (fitnessCenterId.HasValue)
            {
                query = query.Where(t => t.FitnessCenterId == fitnessCenterId.Value);
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.FitnessCenterId = fitnessCenterId;
            ViewBag.FitnessCenters = new SelectList(await _context.FitnessCenters.OrderBy(fc => fc.Name).ToListAsync(), "Id", "Name", fitnessCenterId);

            return View(await query.OrderBy(t => t.FirstName).ThenBy(t => t.LastName).ToListAsync());
        }

        public IActionResult CreateTrainer()
        {
            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name");
            ViewData["ServiceIds"] = new MultiSelectList(_context.Services, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrainer(Trainer trainer, int[] selectedServices, IFormCollection form)
        {
            if (ModelState.IsValid)
            {
                _context.Add(trainer);
                await _context.SaveChangesAsync();

                // Add expertise
                if (selectedServices != null)
                {
                    foreach (var serviceId in selectedServices)
                    {
                        _context.TrainerExpertises.Add(new TrainerExpertise
                        {
                            TrainerId = trainer.Id,
                            ServiceId = serviceId
                        });
                    }
                }

                // Add working hours from form collection
                var daysOfWeek = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>();
                foreach (var day in daysOfWeek)
                {
                    var dayIndex = (int)day;
                    var startTimeKey = $"workingHours[{dayIndex}].StartTime";
                    var endTimeKey = $"workingHours[{dayIndex}].EndTime";
                    
                    if (form.ContainsKey(startTimeKey) && form.ContainsKey(endTimeKey))
                    {
                        var startTimeStr = form[startTimeKey].ToString();
                        var endTimeStr = form[endTimeKey].ToString();
                        
                        if (!string.IsNullOrEmpty(startTimeStr) && !string.IsNullOrEmpty(endTimeStr) &&
                            TimeSpan.TryParse(startTimeStr, out var startTime) &&
                            TimeSpan.TryParse(endTimeStr, out var endTime))
                        {
                            _context.WorkingHours.Add(new WorkingHours
                            {
                                TrainerId = trainer.Id,
                                FitnessCenterId = null,
                                DayOfWeek = day,
                                StartTime = startTime,
                                EndTime = endTime
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Trainers));
            }
            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name", trainer.FitnessCenterId);
            ViewData["ServiceIds"] = new MultiSelectList(_context.Services, "Id", "Name", selectedServices);
            return View(trainer);
        }

        public async Task<IActionResult> EditTrainer(int? id)
        {
            if (id == null) return NotFound();
            var trainer = await _context.Trainers
                .Include(t => t.TrainerExpertises)
                .Include(t => t.WorkingHours)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (trainer == null) return NotFound();

            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name", trainer.FitnessCenterId);
            ViewData["ServiceIds"] = new MultiSelectList(_context.Services, "Id", "Name", trainer.TrainerExpertises.Select(te => te.ServiceId));
            return View(trainer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTrainer(int id, Trainer trainer, int[] selectedServices, IFormCollection form)
        {
            if (id != trainer.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(trainer);

                    // Update expertise
                    var existingExpertise = await _context.TrainerExpertises
                        .Where(te => te.TrainerId == trainer.Id)
                        .ToListAsync();
                    _context.TrainerExpertises.RemoveRange(existingExpertise);

                    if (selectedServices != null)
                    {
                        foreach (var serviceId in selectedServices)
                        {
                            _context.TrainerExpertises.Add(new TrainerExpertise
                            {
                                TrainerId = trainer.Id,
                                ServiceId = serviceId
                            });
                        }
                    }

                    // Update working hours from form collection
                    var existingWorkingHours = await _context.WorkingHours
                        .Where(wh => wh.TrainerId == trainer.Id)
                        .ToListAsync();
                    _context.WorkingHours.RemoveRange(existingWorkingHours);

                    var daysOfWeek = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>();
                    foreach (var day in daysOfWeek)
                    {
                        var dayIndex = (int)day;
                        var startTimeKey = $"workingHours[{dayIndex}].StartTime";
                        var endTimeKey = $"workingHours[{dayIndex}].EndTime";
                        
                        if (form.ContainsKey(startTimeKey) && form.ContainsKey(endTimeKey))
                        {
                            var startTimeStr = form[startTimeKey].ToString();
                            var endTimeStr = form[endTimeKey].ToString();
                            
                            if (!string.IsNullOrEmpty(startTimeStr) && !string.IsNullOrEmpty(endTimeStr) &&
                                TimeSpan.TryParse(startTimeStr, out var startTime) &&
                                TimeSpan.TryParse(endTimeStr, out var endTime))
                            {
                                _context.WorkingHours.Add(new WorkingHours
                                {
                                    TrainerId = trainer.Id,
                                    FitnessCenterId = null,
                                    DayOfWeek = day,
                                    StartTime = startTime,
                                    EndTime = endTime
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TrainerExists(trainer.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Trainers));
            }
            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name", trainer.FitnessCenterId);
            ViewData["ServiceIds"] = new MultiSelectList(_context.Services, "Id", "Name", selectedServices);
            return View(trainer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrainer(int id)
        {
            var trainer = await _context.Trainers.FindAsync(id);
            if (trainer != null)
            {
                _context.Trainers.Remove(trainer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Trainers));
        }

        private bool TrainerExists(int id) => _context.Trainers.Any(e => e.Id == id);
        #endregion
    }
}

