using FitnessCenter.Data;
using FitnessCenter.Models;
using FitnessCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessCenter.Controllers
{
    [Authorize(Roles = "Member")]
    public class AIController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Member> _userManager;
        private readonly IAIService _aiService;
        private readonly ILogger<AIController> _logger;
        private readonly IWebHostEnvironment _environment;

        public AIController(
            ApplicationDbContext context,
            UserManager<Member> userManager,
            IAIService aiService,
            ILogger<AIController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _aiService = aiService;
            _logger = logger;
            _environment = environment;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnalyzePhoto(IFormFile photo)
        {
            if (photo == null || photo.Length == 0)
            {
                ModelState.AddModelError("", "Please select a photo to upload.");
                return View("Index");
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                ModelState.AddModelError("", "Please upload a valid image file (JPG, PNG, or GIF).");
                return View("Index");
            }

            // Validate file size (max 10MB)
            if (photo.Length > 10 * 1024 * 1024)
            {
                ModelState.AddModelError("", "File size must be less than 10MB.");
                return View("Index");
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                // Save photo
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "ai-photos");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{user.Id}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await photo.CopyToAsync(fileStream);
                }

                // Analyze photo with AI
                using (var photoStream = new FileStream(filePath, FileMode.Open))
                {
                    var recommendations = await _aiService.AnalyzePhotoAndGetRecommendationsAsync(photoStream, photo.FileName);

                    ViewBag.PhotoPath = $"/uploads/ai-photos/{uniqueFileName}";
                    ViewBag.Recommendations = recommendations;
                    ViewBag.Success = true;

                    return View("Recommendations");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing photo");
                ModelState.AddModelError("", "An error occurred while analyzing the photo. Please try again.");
                return View("Index");
            }
        }

        public IActionResult Recommendations()
        {
            return View();
        }
    }
}

