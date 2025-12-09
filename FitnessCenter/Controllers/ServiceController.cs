using FitnessCenter.Data;
using FitnessCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IActionResult> Index()
        {
            var services = await _context.Services
                .Include(s => s.FitnessCenter)
                .ToListAsync();
            return View(services);
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

            return View(service);
        }
    }
}

