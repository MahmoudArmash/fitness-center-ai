using FitnessCenter.Constants;
using FitnessCenter.DTOs;
using FitnessCenter.Helpers;
using FitnessCenter.Models;
using FitnessCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Controllers
{
    [Authorize(Roles = "Member")]
    public class AIController : Controller
    {
        private readonly UserManager<Member> _userManager;
        private readonly IAIService _aiService;
        private readonly IFileValidationService _fileValidationService;
        private readonly ILogger<AIController> _logger;
        private readonly IWebHostEnvironment _environment;

        public AIController(
            UserManager<Member> userManager,
            IAIService aiService,
            IFileValidationService fileValidationService,
            ILogger<AIController> logger,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _aiService = aiService;
            _fileValidationService = fileValidationService;
            _logger = logger;
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            var user = await UserDataHelper.GetCurrentUserAsync(_userManager, User);
            UserDataHelper.SetUserViewBagData(ViewBag, user);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnalyzePhoto(
            IFormFile photo, 
            decimal? height, 
            decimal? weight, 
            string? gender, 
            DateTime? dateOfBirth, 
            bool useProfileMetrics = true)
        {
            _logger.LogInformation("AnalyzePhoto started - File: {FileName}, Size: {Size} bytes", 
                photo?.FileName, photo?.Length);

            // Validate file
            var (isValid, errorMessage) = _fileValidationService.ValidateImageFile(photo);
            if (!isValid)
            {
                _logger.LogWarning("File validation failed: {Error}", errorMessage);
                ModelState.AddModelError("", errorMessage!);
                await SetUserViewBagDataAsync();
                return View("Index");
            }

            try
            {
                var user = await UserDataHelper.GetCurrentUserAsync(_userManager, User);
                if (user == null)
                {
                    _logger.LogError("User not found");
                    return NotFound();
                }

                var metrics = BodyMetricsHelper.GetFinalMetrics(
                    height, weight, gender, dateOfBirth, user, useProfileMetrics);

                // Save photo
                var photoPath = await FileUploadHelper.SaveFileAsync(
                    photo!, 
                    Path.Combine(AppConstants.UploadsFolder, AppConstants.AIPhotosFolder), 
                    user.Id, 
                    _environment);

                // Analyze photo with AI
                var filePath = Path.Combine(_environment.WebRootPath, photoPath.TrimStart('/'));
                using (var photoStream = new FileStream(filePath, FileMode.Open))
                {
                    string recommendations = metrics.HasAnyMetrics
                        ? await _aiService.AnalyzePhotoWithBodyMetricsAsync(
                            photoStream, photo!.FileName, metrics.Height, metrics.Weight, metrics.Gender, metrics.DateOfBirth)
                        : await _aiService.AnalyzePhotoAndGetRecommendationsAsync(photoStream, photo!.FileName);

                    ViewBag.PhotoPath = photoPath;
                    ViewBag.Recommendations = recommendations;
                    ViewBag.Success = true;

                    _logger.LogInformation("AnalyzePhoto completed successfully");
                    return View("Recommendations");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AnalyzePhoto");
                ModelState.AddModelError("", GetErrorMessage(ex));
                await SetUserViewBagDataAsync();
                return View("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DietPlan()
        {
            await SetUserViewBagDataAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetDietPlan(
            decimal? height, 
            decimal? weight, 
            string? gender, 
            DateTime? dateOfBirth, 
            string? fitnessGoal, 
            bool useProfileMetrics = true)
        {
            try
            {
                var user = await UserDataHelper.GetCurrentUserAsync(_userManager, User);
                if (user == null) return NotFound();

                var metrics = BodyMetricsHelper.GetFinalMetrics(
                    height, weight, gender, dateOfBirth, user, useProfileMetrics);

                if (!metrics.Height.HasValue || !metrics.Weight.HasValue)
                {
                    ModelState.AddModelError("", AppConstants.ErrorHeightWeightRequired);
                    SetMetricsViewBag(metrics, fitnessGoal);
                    return View("DietPlan");
                }

                var dietPlan = await _aiService.GetDietPlanRecommendationsAsync(
                    metrics.Height, metrics.Weight, metrics.Gender, metrics.DateOfBirth, fitnessGoal);

                ViewBag.DietPlan = dietPlan;
                ViewBag.Success = true;
                ViewBag.Height = metrics.Height;
                ViewBag.Weight = metrics.Weight;
                ViewBag.Gender = metrics.Gender;
                ViewBag.FitnessGoal = fitnessGoal;

                return View("DietPlanRecommendations");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating diet plan");
                ModelState.AddModelError("", "An error occurred while generating the diet plan. Please try again.");
                await SetUserViewBagDataAsync();
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
            await SetUserViewBagDataAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateExerciseVisualization(
            string exerciseName, 
            decimal? height, 
            decimal? weight, 
            string? gender, 
            IFormFile? photo, 
            bool useProfileMetrics = true)
        {
            if (string.IsNullOrWhiteSpace(exerciseName))
            {
                ModelState.AddModelError("", AppConstants.ErrorExerciseNameRequired);
                await SetUserViewBagDataAsync();
                return View("ExerciseVisualization");
            }

            try
            {
                var user = await UserDataHelper.GetCurrentUserAsync(_userManager, User);
                if (user == null) return NotFound();

                var metrics = BodyMetricsHelper.GetFinalMetrics(
                    height, weight, gender, null, user, useProfileMetrics);

                Stream? photoStream = null;
                string? photoFileName = null;

                // Handle photo upload or use profile photo
                if (photo != null && photo.Length > 0)
                {
                    var (isValid, errorMessage) = _fileValidationService.ValidateImageFile(photo);
                    if (!isValid)
                    {
                        ModelState.AddModelError("", errorMessage!);
                        await SetUserViewBagDataAsync();
                        return View("ExerciseVisualization");
                    }

                    photoStream = photo.OpenReadStream();
                    photoFileName = photo.FileName;
                }
                else if (!string.IsNullOrEmpty(user.PhotoPath))
                {
                    var profilePhotoPath = Path.Combine(_environment.WebRootPath, user.PhotoPath.TrimStart('/'));
                    if (System.IO.File.Exists(profilePhotoPath))
                    {
                        photoStream = new FileStream(profilePhotoPath, FileMode.Open, FileAccess.Read);
                        photoFileName = Path.GetFileName(profilePhotoPath);
                    }
                }

                try
                {
                    var visualizationDescription = await _aiService.GenerateExerciseVisualizationImageAsync(
                        exerciseName, metrics.Height, metrics.Weight, metrics.Gender, photoStream, photoFileName);

                    ViewBag.ExerciseName = exerciseName;
                    ViewBag.VisualizationDescription = visualizationDescription;
                    ViewBag.Height = metrics.Height;
                    ViewBag.Weight = metrics.Weight;
                    ViewBag.Gender = metrics.Gender;
                    ViewBag.Success = true;

                    return View("ExerciseVisualizationResult");
                }
                finally
                {
                    photoStream?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating exercise visualization");
                ModelState.AddModelError("", "An error occurred while generating the exercise visualization. Please try again.");
                await SetUserViewBagDataAsync();
                return View("ExerciseVisualization");
            }
        }

        public IActionResult ExerciseVisualizationResult()
        {
            return View();
        }

        // Helper methods
        private async Task SetUserViewBagDataAsync()
        {
            var user = await UserDataHelper.GetCurrentUserAsync(_userManager, User);
            UserDataHelper.SetUserViewBagData(ViewBag, user);
        }

        private void SetMetricsViewBag(BodyMetricsDto metrics, string? fitnessGoal)
        {
            ViewBag.UserHeight = metrics.Height;
            ViewBag.UserWeight = metrics.Weight;
            ViewBag.UserGender = metrics.Gender;
            ViewBag.UserDateOfBirth = metrics.DateOfBirth;
            ViewBag.FitnessGoal = fitnessGoal;
        }

        private static string GetErrorMessage(Exception ex)
        {
            return ex switch
            {
                HttpRequestException httpEx when httpEx.Message.Contains("leaked", StringComparison.OrdinalIgnoreCase) => 
                    httpEx.Message,
                HttpRequestException httpEx => 
                    $"Unable to connect to AI service. {httpEx.Message} Please check your internet connection and API configuration.",
                InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("API key") => 
                    $"⚠️ API Key Configuration Required: {invalidOpEx.Message} " +
                    $"<br/><small>Quick fix: Add your API key to Properties/launchSettings.json in the GOOGLE_GEMINI_API_KEY field, or set it as an environment variable.</small>",
                InvalidOperationException invalidOpEx => 
                    $"Configuration error: {invalidOpEx.Message}",
                _ => 
                    $"An error occurred: {ex.Message} Please check the console logs for details."
            };
        }
    }
}

