using FitnessCenter.Data;
using FitnessCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

        public IActionResult Index()
        {
            ViewBag.TotalMembers = _context.Users.Count();
            ViewBag.TotalTrainers = _context.Trainers.Count();
            ViewBag.TotalAppointments = _context.Appointments.Count();
            ViewBag.PendingAppointments = _context.Appointments.Count(a => a.Status == AppointmentStatus.Pending);
            ViewBag.TotalServices = _context.Services.Count();

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
        public async Task<IActionResult> Services()
        {
            return View(await _context.Services.Include(s => s.FitnessCenter).ToListAsync());
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
        public async Task<IActionResult> Trainers()
        {
            return View(await _context.Trainers.Include(t => t.FitnessCenter).ToListAsync());
        }

        public IActionResult CreateTrainer()
        {
            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name");
            ViewData["ServiceIds"] = new MultiSelectList(_context.Services, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrainer(Trainer trainer, int[] selectedServices)
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
                    await _context.SaveChangesAsync();
                }

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
                .FirstOrDefaultAsync(t => t.Id == id);
            if (trainer == null) return NotFound();

            ViewData["FitnessCenterId"] = new SelectList(_context.FitnessCenters, "Id", "Name", trainer.FitnessCenterId);
            ViewData["ServiceIds"] = new MultiSelectList(_context.Services, "Id", "Name", trainer.TrainerExpertises.Select(te => te.ServiceId));
            return View(trainer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTrainer(int id, Trainer trainer, int[] selectedServices)
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

