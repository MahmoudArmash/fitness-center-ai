using FitnessCenter.Data;
using FitnessCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessCenter.Controllers
{
    [Authorize]
    public class MemberController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Member> _userManager;
        private readonly ILogger<MemberController> _logger;
        private readonly IWebHostEnvironment _environment;

        public MemberController(
            ApplicationDbContext context,
            UserManager<Member> userManager,
            ILogger<MemberController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
        }

        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var appointments = await _context.Appointments
                .Where(a => a.MemberId == user.Id)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .OrderByDescending(a => a.AppointmentDateTime)
                .Take(5)
                .ToListAsync();

            ViewBag.RecentAppointments = appointments;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(Member model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (ModelState.IsValid)
            {
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Address = model.Address;
                user.DateOfBirth = model.DateOfBirth;
                user.Gender = model.Gender;
                user.Height = model.Height;
                user.Weight = model.Weight;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Profile updated successfully.";
                    return RedirectToAction(nameof(Profile));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View("Profile", user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhoto(IFormFile photo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (photo != null && photo.Length > 0)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "photos");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{user.Id}_{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await photo.CopyToAsync(fileStream);
                }

                // Delete old photo if exists
                if (!string.IsNullOrEmpty(user.PhotoPath))
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, user.PhotoPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                user.PhotoPath = $"/uploads/photos/{uniqueFileName}";
                await _userManager.UpdateAsync(user);

                TempData["SuccessMessage"] = "Photo uploaded successfully.";
            }

            return RedirectToAction(nameof(Profile));
        }
    }
}

