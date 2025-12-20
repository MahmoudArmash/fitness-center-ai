using FitnessCenter.Data;
using FitnessCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FitnessCenter.Controllers
{
    [Authorize]
    public class ServiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServiceController> _logger;

        public ServiceController(ApplicationDbContext context, ILogger<ServiceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string searchTerm, string serviceType, int? fitnessCenterId, string sortBy = "name")
        {
            // Build LINQ query with filtering
            var query = _context.Services
                .Include(s => s.FitnessCenter)
                .AsQueryable();

            // Apply LINQ Where filters (case-insensitive)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(searchLower) || 
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

            // Apply LINQ OrderBy sorting
            var services = sortBy.ToLower() switch
            {
                "price" => query.OrderBy(s => s.Price),
                "price_desc" => query.OrderByDescending(s => s.Price),
                "duration" => query.OrderBy(s => s.DurationMinutes),
                "duration_desc" => query.OrderByDescending(s => s.DurationMinutes),
                "name_desc" => query.OrderByDescending(s => s.Name),
                _ => query.OrderBy(s => s.Name)
            };

            // Get statistics using LINQ aggregations
            var allServices = await _context.Services.ToListAsync();
            ViewBag.ServiceStats = new
            {
                TotalServices = allServices.Count,
                AveragePrice = allServices.Any() ? allServices.Average(s => s.Price) : 0,
                MinPrice = allServices.Any() ? allServices.Min(s => s.Price) : 0,
                MaxPrice = allServices.Any() ? allServices.Max(s => s.Price) : 0,
                ServiceTypes = allServices.GroupBy(s => s.Type)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList()
            };

            // Get distinct service types for filter dropdown using LINQ
            ViewBag.ServiceTypes = await _context.Services
                .Select(s => s.Type)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            // Get fitness centers for filter dropdown
            ViewBag.FitnessCenters = await _context.FitnessCenters
                .OrderBy(fc => fc.Name)
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.ServiceType = serviceType;
            ViewBag.FitnessCenterId = fitnessCenterId;
            ViewBag.SortBy = sortBy;

            return View(await services.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var service = await _context.Services
                .Include(s => s.FitnessCenter)
                .Include(s => s.TrainerExpertises)
                    .ThenInclude(te => te.Trainer)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (service == null) return NotFound();

            // Use LINQ to get related statistics
            var appointments = await _context.Appointments
                .Where(a => a.ServiceId == id)
                .ToListAsync();

            ViewBag.ServiceStatistics = new
            {
                TotalAppointments = appointments.Count,
                CompletedAppointments = appointments.Count(a => a.Status == AppointmentStatus.Completed),
                PendingAppointments = appointments.Count(a => a.Status == AppointmentStatus.Pending),
                TotalRevenue = appointments
                    .Where(a => a.Status == AppointmentStatus.Completed)
                    .Sum(a => a.Price),
                AverageRating = appointments
                    .Where(a => a.Status == AppointmentStatus.Completed)
                    .Any() ? appointments
                        .Where(a => a.Status == AppointmentStatus.Completed)
                        .Average(a => (double?)a.Price) : 0
            };

            // Get trainers for this service using LINQ
            var trainers = service.TrainerExpertises
                .Select(te => te.Trainer)
                .Distinct()
                .OrderBy(t => t.FullName)
                .ToList();

            ViewBag.Trainers = trainers;

            return View(service);
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceStatistics()
        {
            var services = await _context.Services
                .Include(s => s.Appointments)
                .ToListAsync();

            // Use LINQ GroupBy for statistics
            var statistics = services
                .GroupBy(s => s.Type)
                .Select(g => new
                {
                    ServiceType = g.Key,
                    Count = g.Count(),
                    AveragePrice = g.Average(s => s.Price),
                    TotalAppointments = g.SelectMany(s => s.Appointments).Count(),
                    TotalRevenue = g.SelectMany(s => s.Appointments)
                        .Where(a => a.Status == AppointmentStatus.Completed)
                        .Sum(a => a.Price)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            return Json(statistics);
        }
    }
}

