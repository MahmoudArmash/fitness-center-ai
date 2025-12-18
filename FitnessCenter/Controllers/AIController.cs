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

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ViewBag.UserHeight = user.Height;
                ViewBag.UserWeight = user.Weight;
                ViewBag.UserGender = user.Gender;
                ViewBag.UserDateOfBirth = user.DateOfBirth;
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnalyzePhoto(IFormFile photo, decimal? height, decimal? weight, string? gender, DateTime? dateOfBirth, bool useProfileMetrics = true)
        {
            _logger.LogInformation("=== AnalyzePhoto Action Started ===");
            _logger.LogInformation("Photo: {FileName}, Size: {Size} bytes, UseProfileMetrics: {UseProfileMetrics}", 
                photo?.FileName, photo?.Length, useProfileMetrics);
            _logger.LogInformation("Body Metrics - Height: {Height}, Weight: {Weight}, Gender: {Gender}, DateOfBirth: {DateOfBirth}",
                height, weight, gender, dateOfBirth);

            if (photo == null || photo.Length == 0)
            {
                _logger.LogWarning("No photo provided");
                ModelState.AddModelError("", "Please select a photo to upload.");
                return View("Index");
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            _logger.LogInformation("File extension: {Extension}", fileExtension);
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                _logger.LogWarning("Invalid file extension: {Extension}", fileExtension);
                ModelState.AddModelError("", "Please upload a valid image file (JPG, PNG, or GIF).");
                return View("Index");
            }

            // Validate file size (max 10MB)
            if (photo.Length > 10 * 1024 * 1024)
            {
                _logger.LogWarning("File too large: {Size} bytes", photo.Length);
                ModelState.AddModelError("", "File size must be less than 10MB.");
                return View("Index");
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogError("User not found");
                    return NotFound();
                }

                _logger.LogInformation("User: {UserId}, Email: {Email}", user.Id, user.Email);

                // Get body metrics - use form input if provided, otherwise use profile data
                decimal? finalHeight = height;
                decimal? finalWeight = weight;
                string? finalGender = gender;
                DateTime? finalDateOfBirth = dateOfBirth;

                if (useProfileMetrics)
                {
                    _logger.LogInformation("Using profile metrics - Profile Height: {Height}, Weight: {Weight}, Gender: {Gender}",
                        user.Height, user.Weight, user.Gender);
                    finalHeight ??= user.Height;
                    finalWeight ??= user.Weight;
                    finalGender ??= user.Gender;
                    finalDateOfBirth ??= user.DateOfBirth;
                }

                _logger.LogInformation("Final metrics - Height: {Height}, Weight: {Weight}, Gender: {Gender}, DateOfBirth: {DateOfBirth}",
                    finalHeight, finalWeight, finalGender, finalDateOfBirth);

                // Save photo
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "ai-photos");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    _logger.LogInformation("Created uploads folder: {Folder}", uploadsFolder);
                }

                var uniqueFileName = $"{user.Id}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                _logger.LogInformation("Saving photo to: {FilePath}", filePath);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await photo.CopyToAsync(fileStream);
                }
                _logger.LogInformation("Photo saved successfully");

                // Analyze photo with AI (with or without body metrics)
                using (var photoStream = new FileStream(filePath, FileMode.Open))
                {
                    string recommendations;
                    
                    if (finalHeight.HasValue || finalWeight.HasValue || !string.IsNullOrEmpty(finalGender) || finalDateOfBirth.HasValue)
                    {
                        _logger.LogInformation("Calling AnalyzePhotoWithBodyMetricsAsync");
                        recommendations = await _aiService.AnalyzePhotoWithBodyMetricsAsync(
                            photoStream, photo.FileName, finalHeight, finalWeight, finalGender, finalDateOfBirth);
                    }
                    else
                    {
                        _logger.LogInformation("Calling AnalyzePhotoAndGetRecommendationsAsync (no body metrics)");
                        recommendations = await _aiService.AnalyzePhotoAndGetRecommendationsAsync(photoStream, photo.FileName);
                    }

                    _logger.LogInformation("Recommendations received - Length: {Length} characters", recommendations?.Length ?? 0);

                    ViewBag.PhotoPath = $"/uploads/ai-photos/{uniqueFileName}";
                    ViewBag.Recommendations = recommendations;
                    ViewBag.Success = true;

                    _logger.LogInformation("=== AnalyzePhoto Action Completed Successfully ===");
                    return View("Recommendations");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR in AnalyzePhoto Action ===");
                _logger.LogError("Exception Type: {Type}", ex.GetType().Name);
                _logger.LogError("Exception Message: {Message}", ex.Message);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner Exception: {InnerException}", ex.InnerException.ToString());
                }
                
                // Provide user-friendly error message based on exception type
                string errorMessage = ex switch
                {
                    HttpRequestException httpEx when httpEx.Message.Contains("leaked", StringComparison.OrdinalIgnoreCase) => 
                        httpEx.Message, // Use the detailed message from AIService about leaked API key
                    HttpRequestException httpEx => 
                        $"Unable to connect to AI service. {httpEx.Message} Please check your internet connection and API configuration.",
                    InvalidOperationException invalidOpEx => 
                        $"Configuration error: {invalidOpEx.Message}",
                    _ => 
                        $"An error occurred while analyzing the photo: {ex.Message} Please check the console logs for details."
                };
                
                ModelState.AddModelError("", errorMessage);
                
                // Reload user data for the view
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    ViewBag.UserHeight = user.Height;
                    ViewBag.UserWeight = user.Weight;
                    ViewBag.UserGender = user.Gender;
                    ViewBag.UserDateOfBirth = user.DateOfBirth;
                }
                
                return View("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DietPlan()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ViewBag.UserHeight = user.Height;
                ViewBag.UserWeight = user.Weight;
                ViewBag.UserGender = user.Gender;
                ViewBag.UserDateOfBirth = user.DateOfBirth;
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetDietPlan(decimal? height, decimal? weight, string? gender, DateTime? dateOfBirth, string? fitnessGoal, bool useProfileMetrics = true)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                // Get body metrics - use form input if provided, otherwise use profile data
                decimal? finalHeight = height;
                decimal? finalWeight = weight;
                string? finalGender = gender;
                DateTime? finalDateOfBirth = dateOfBirth;

                if (useProfileMetrics)
                {
                    finalHeight ??= user.Height;
                    finalWeight ??= user.Weight;
                    finalGender ??= user.Gender;
                    finalDateOfBirth ??= user.DateOfBirth;
                }

                if (!finalHeight.HasValue || !finalWeight.HasValue)
                {
                    ModelState.AddModelError("", "Height and weight are required for diet plan recommendations.");
                    ViewBag.UserHeight = finalHeight;
                    ViewBag.UserWeight = finalWeight;
                    ViewBag.UserGender = finalGender;
                    ViewBag.UserDateOfBirth = finalDateOfBirth;
                    ViewBag.FitnessGoal = fitnessGoal;
                    return View("DietPlan");
                }

                var dietPlan = await _aiService.GetDietPlanRecommendationsAsync(
                    finalHeight, finalWeight, finalGender, finalDateOfBirth, fitnessGoal);

                ViewBag.DietPlan = dietPlan;
                ViewBag.Success = true;
                ViewBag.Height = finalHeight;
                ViewBag.Weight = finalWeight;
                ViewBag.Gender = finalGender;
                ViewBag.FitnessGoal = fitnessGoal;

                return View("DietPlanRecommendations");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating diet plan");
                ModelState.AddModelError("", "An error occurred while generating the diet plan. Please try again.");
                return View("DietPlan");
            }
        }

        public IActionResult Recommendations()
        {
            return View();
        }

        public IActionResult DietPlanRecommendations()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExerciseVisualization()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ViewBag.UserHeight = user.Height;
                ViewBag.UserWeight = user.Weight;
                ViewBag.UserGender = user.Gender;
                ViewBag.UserDateOfBirth = user.DateOfBirth;
                ViewBag.UserPhotoPath = user.PhotoPath;
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateExerciseVisualization(string exerciseName, decimal? height, decimal? weight, string? gender, IFormFile? photo, bool useProfileMetrics = true)
        {
            if (string.IsNullOrWhiteSpace(exerciseName))
            {
                ModelState.AddModelError("", "Exercise name is required.");
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    ViewBag.UserHeight = user.Height;
                    ViewBag.UserWeight = user.Weight;
                    ViewBag.UserGender = user.Gender;
                    ViewBag.UserDateOfBirth = user.DateOfBirth;
                    ViewBag.UserPhotoPath = user.PhotoPath;
                }
                return View("ExerciseVisualization");
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                // Get body metrics - use form input if provided, otherwise use profile data
                decimal? finalHeight = height;
                decimal? finalWeight = weight;
                string? finalGender = gender;

                if (useProfileMetrics)
                {
                    finalHeight ??= user.Height;
                    finalWeight ??= user.Weight;
                    finalGender ??= user.Gender;
                }

                Stream? photoStream = null;
                string? photoFileName = null;

                // If user uploaded a photo, use it; otherwise try to use profile photo
                if (photo != null && photo.Length > 0)
                {
                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("", "Please upload a valid image file (JPG, PNG, or GIF).");
                        var userForError = await _userManager.GetUserAsync(User);
                        if (userForError != null)
                        {
                            ViewBag.UserHeight = userForError.Height;
                            ViewBag.UserWeight = userForError.Weight;
                            ViewBag.UserGender = userForError.Gender;
                            ViewBag.UserDateOfBirth = userForError.DateOfBirth;
                            ViewBag.UserPhotoPath = userForError.PhotoPath;
                        }
                        return View("ExerciseVisualization");
                    }

                    photoStream = photo.OpenReadStream();
                    photoFileName = photo.FileName;
                }
                else if (!string.IsNullOrEmpty(user.PhotoPath))
                {
                    // Try to use profile photo
                    var profilePhotoPath = Path.Combine(_environment.WebRootPath, user.PhotoPath.TrimStart('/'));
                    if (System.IO.File.Exists(profilePhotoPath))
                    {
                        photoStream = new FileStream(profilePhotoPath, FileMode.Open, FileAccess.Read);
                        photoFileName = Path.GetFileName(profilePhotoPath);
                    }
                }

                // Generate the exercise visualization description using Google Gemini
                string visualizationDescription;
                try
                {
                    visualizationDescription = await _aiService.GenerateExerciseVisualizationImageAsync(
                        exerciseName, finalHeight, finalWeight, finalGender, photoStream, photoFileName);
                }
                finally
                {
                    photoStream?.Dispose();
                }

                ViewBag.ExerciseName = exerciseName;
                ViewBag.VisualizationDescription = visualizationDescription;
                ViewBag.Height = finalHeight;
                ViewBag.Weight = finalWeight;
                ViewBag.Gender = finalGender;
                ViewBag.Success = true;

                return View("ExerciseVisualizationResult");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating exercise visualization");
                ModelState.AddModelError("", "An error occurred while generating the exercise visualization. Please try again.");
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    ViewBag.UserHeight = user.Height;
                    ViewBag.UserWeight = user.Weight;
                    ViewBag.UserGender = user.Gender;
                    ViewBag.UserDateOfBirth = user.DateOfBirth;
                    ViewBag.UserPhotoPath = user.PhotoPath;
                }
                return View("ExerciseVisualization");
            }
        }

        public IActionResult ExerciseVisualizationResult()
        {
            return View();
        }
    }
}

