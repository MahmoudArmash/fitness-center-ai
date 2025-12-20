using FitnessCenter.DTOs;
using FitnessCenter.Helpers;

namespace FitnessCenter.Services
{
    public interface IAIService
    {
        Task<string> AnalyzePhotoAndGetRecommendationsAsync(Stream photoStream, string fileName);
        Task<string> AnalyzePhotoWithBodyMetricsAsync(Stream photoStream, string fileName, decimal? height, decimal? weight, string? gender, DateTime? dateOfBirth);
        Task<string> GetDietPlanRecommendationsAsync(decimal? height, decimal? weight, string? gender, DateTime? dateOfBirth, string? fitnessGoal);
        Task<string> GenerateExerciseVisualizationImageAsync(string exerciseName, decimal? height, decimal? weight, string? gender, Stream? userPhotoStream = null, string? photoFileName = null);
    }

    public class AIService : IAIService
    {
        private readonly IGeminiApiClient _apiClient;
        private readonly ILogger<AIService> _logger;

        public AIService(IGeminiApiClient apiClient, ILogger<AIService> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public async Task<string> AnalyzePhotoAndGetRecommendationsAsync(Stream photoStream, string fileName)
        {
            var mimeType = FileUploadHelper.GetMimeType(fileName);
            var prompt = PromptBuilder.BuildPhotoAnalysisPrompt();
            return await _apiClient.GenerateContentAsync(prompt, photoStream, mimeType);
        }

        public async Task<string> AnalyzePhotoWithBodyMetricsAsync(
            Stream photoStream, 
            string fileName, 
            decimal? height, 
            decimal? weight, 
            string? gender, 
            DateTime? dateOfBirth)
        {
            var mimeType = FileUploadHelper.GetMimeType(fileName);
            var metrics = new BodyMetricsDto
            {
                Height = height,
                Weight = weight,
                Gender = gender,
                DateOfBirth = dateOfBirth
            };
            var prompt = PromptBuilder.BuildPhotoAnalysisPrompt(metrics);
            return await _apiClient.GenerateContentAsync(prompt, photoStream, mimeType);
        }

        public async Task<string> GetDietPlanRecommendationsAsync(
            decimal? height, 
            decimal? weight, 
            string? gender, 
            DateTime? dateOfBirth, 
            string? fitnessGoal)
        {
            var metrics = new BodyMetricsDto
            {
                Height = height,
                Weight = weight,
                Gender = gender,
                DateOfBirth = dateOfBirth
            };
            var prompt = PromptBuilder.BuildDietPlanPrompt(metrics, fitnessGoal);
            return await _apiClient.GenerateContentAsync(prompt);
        }

        public async Task<string> GenerateExerciseVisualizationImageAsync(
            string exerciseName, 
            decimal? height, 
            decimal? weight, 
            string? gender, 
            Stream? userPhotoStream = null, 
            string? photoFileName = null)
        {
            try
            {
                string? bodyDescription = null;
                var metrics = new BodyMetricsDto
                {
                    Height = height,
                    Weight = weight,
                    Gender = gender
                };

                // Analyze user photo if provided
                if (userPhotoStream != null && !string.IsNullOrEmpty(photoFileName))
                {
                    try
                    {
                        var mimeType = FileUploadHelper.GetMimeType(photoFileName);
                        if (userPhotoStream.CanSeek)
                        {
                            userPhotoStream.Position = 0;
                        }

                        var analysisPrompt = PromptBuilder.BuildBodyAnalysisPrompt();
                        bodyDescription = await _apiClient.GenerateContentAsync(analysisPrompt, userPhotoStream, mimeType);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to analyze user photo, continuing with body metrics only");
                    }
                }

                var prompt = PromptBuilder.BuildExerciseVisualizationPrompt(exerciseName, metrics, bodyDescription);
                return await _apiClient.GenerateContentAsync(prompt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating exercise visualization");
                throw new Exception("Failed to generate exercise visualization using Google AI.", ex);
            }
        }
    }
}

