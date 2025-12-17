using System.Text;
using System.Text.Json;

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
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIService> _logger;

        public AIService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<AIService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        private async Task<string> CallGeminiAPIAsync(string prompt, Stream? photoStream = null, string? mimeType = null)
        {
            var apiKey = _configuration["GoogleGemini:ApiKey"];
            var model = _configuration["GoogleGemini:Model"] ?? "gemini-1.5-flash";

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Google Gemini API key is not configured.");
            }

            try
            {
                var parts = new List<object>
                {
                    new { text = prompt }
                };

                // Add image if provided
                if (photoStream != null && !string.IsNullOrEmpty(mimeType))
                {
                    using var memoryStream = new MemoryStream();
                    await photoStream.CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();
                    var base64Image = Convert.ToBase64String(imageBytes);

                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = mimeType,
                            data = base64Image
                        }
                    });
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = parts
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonDocument.Parse(responseContent);

                var recommendations = responseJson.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return recommendations ?? "Unable to generate recommendations.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Google Gemini API");
                throw new Exception("Failed to generate recommendations from AI service.", ex);
            }
        }

        public async Task<string> AnalyzePhotoAndGetRecommendationsAsync(Stream photoStream, string fileName)
        {
            var mimeType = GetMimeType(fileName);
            var prompt = "Analyze this fitness photo and provide personalized exercise recommendations. Consider body type, posture, and suggest appropriate exercises, workout routines, and fitness goals. Be specific and actionable. Provide recommendations in a clear, structured format.";

            return await CallGeminiAPIAsync(prompt, photoStream, mimeType);
        }

        public async Task<string> AnalyzePhotoWithBodyMetricsAsync(Stream photoStream, string fileName, decimal? height, decimal? weight, string? gender, DateTime? dateOfBirth)
        {
            var mimeType = GetMimeType(fileName);
            var age = dateOfBirth.HasValue ? (int)((DateTime.Now - dateOfBirth.Value).TotalDays / 365.25) : (int?)null;

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Analyze this fitness photo and provide personalized exercise recommendations. Consider body type, posture, and suggest appropriate exercises, workout routines, and fitness goals. Be specific and actionable.");

            if (height.HasValue || weight.HasValue || !string.IsNullOrEmpty(gender) || age.HasValue)
            {
                promptBuilder.AppendLine("\nAdditional user information:");
                if (height.HasValue) promptBuilder.AppendLine($"- Height: {height} cm");
                if (weight.HasValue) promptBuilder.AppendLine($"- Weight: {weight} kg");
                if (!string.IsNullOrEmpty(gender)) promptBuilder.AppendLine($"- Gender: {gender}");
                if (age.HasValue) promptBuilder.AppendLine($"- Age: {age} years");

                promptBuilder.AppendLine("\nUse this information along with the photo analysis to provide highly personalized recommendations. Consider BMI, body composition, and age-appropriate exercises.");
            }

            promptBuilder.AppendLine("\nProvide recommendations in a clear, structured format with specific exercises, sets, reps, and workout schedules.");

            return await CallGeminiAPIAsync(promptBuilder.ToString(), photoStream, mimeType);
        }

        public async Task<string> GetDietPlanRecommendationsAsync(decimal? height, decimal? weight, string? gender, DateTime? dateOfBirth, string? fitnessGoal)
        {
            var age = dateOfBirth.HasValue ? (int)((DateTime.Now - dateOfBirth.Value).TotalDays / 365.25) : (int?)null;

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Provide a personalized diet and nutrition plan based on the following information:");

            if (height.HasValue) promptBuilder.AppendLine($"- Height: {height} cm");
            if (weight.HasValue) promptBuilder.AppendLine($"- Weight: {weight} kg");
            if (!string.IsNullOrEmpty(gender)) promptBuilder.AppendLine($"- Gender: {gender}");
            if (age.HasValue) promptBuilder.AppendLine($"- Age: {age} years");
            if (!string.IsNullOrEmpty(fitnessGoal)) promptBuilder.AppendLine($"- Fitness Goal: {fitnessGoal}");

            promptBuilder.AppendLine("\nProvide a comprehensive diet plan including:");
            promptBuilder.AppendLine("- Daily calorie recommendations");
            promptBuilder.AppendLine("- Macronutrient breakdown (proteins, carbs, fats)");
            promptBuilder.AppendLine("- Meal suggestions for breakfast, lunch, dinner, and snacks");
            promptBuilder.AppendLine("- Specific food recommendations");
            promptBuilder.AppendLine("- Hydration guidelines");
            promptBuilder.AppendLine("- Any supplements that might be beneficial");

            promptBuilder.AppendLine("\nFormat the response in a clear, structured way that is easy to follow.");

            return await CallGeminiAPIAsync(promptBuilder.ToString());
        }

        public async Task<string> GenerateExerciseVisualizationImageAsync(string exerciseName, decimal? height, decimal? weight, string? gender, Stream? userPhotoStream = null, string? photoFileName = null)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            var model = _configuration["OpenAI:ImageModel"] ?? "dall-e-3";

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured.");
            }

            try
            {
                // If user photo is provided, analyze it with Gemini to get body description
                string? bodyDescription = null;
                if (userPhotoStream != null && !string.IsNullOrEmpty(photoFileName))
                {
                    try
                    {
                        var mimeType = GetMimeType(photoFileName);
                        // Reset stream position if needed
                        if (userPhotoStream.CanSeek)
                        {
                            userPhotoStream.Position = 0;
                        }

                        var analysisPrompt = $"Analyze this person's body type, physique, and physical characteristics. Provide a brief, professional description focusing on body build, muscle definition, and overall physique that would be useful for creating a fitness visualization. Keep it concise (2-3 sentences max).";
                        bodyDescription = await CallGeminiAPIAsync(analysisPrompt, userPhotoStream, mimeType);
                        
                        // Reset stream position again if needed
                        if (userPhotoStream.CanSeek)
                        {
                            userPhotoStream.Position = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to analyze user photo with Gemini, continuing with body metrics only");
                        // Continue without photo analysis
                    }
                }

                // Build the prompt for image generation
                var promptBuilder = new StringBuilder();
                promptBuilder.Append($"A realistic, professional fitness illustration showing a person performing the exercise '{exerciseName}'. ");

                // Use photo analysis if available, otherwise use body metrics
                if (!string.IsNullOrEmpty(bodyDescription))
                {
                    promptBuilder.Append($"The person has the following characteristics: {bodyDescription}. ");
                }
                else
                {
                    // Add body type information if available
                    if (height.HasValue && weight.HasValue)
                    {
                        var bmi = (double)(weight.Value / ((height.Value / 100) * (height.Value / 100)));
                        string bodyType = bmi switch
                        {
                            < 18.5 => "slim",
                            < 25 => "athletic and fit",
                            < 30 => "muscular and strong",
                            _ => "strong and powerful"
                        };
                        promptBuilder.Append($"The person has a {bodyType} body type (height: {height}cm, weight: {weight}kg). ");
                    }
                }

                if (!string.IsNullOrEmpty(gender))
                {
                    promptBuilder.Append($"The person is {gender.ToLower()}. ");
                }

                promptBuilder.Append("The image should show proper form and technique. ");
                promptBuilder.Append("Professional fitness photography style, clean background, well-lit, inspiring and motivational. ");
                promptBuilder.Append("The person should be wearing appropriate workout attire. ");
                promptBuilder.Append("High quality, detailed, realistic illustration.");

                var prompt = promptBuilder.ToString();

                // Call OpenAI DALL-E API
                var requestBody = new
                {
                    model = model,
                    prompt = prompt,
                    n = 1,
                    size = "1024x1024",
                    quality = "standard",
                    response_format = "url"
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var url = "https://api.openai.com/v1/images/generations";
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonDocument.Parse(responseContent);

                var imageUrl = responseJson.RootElement
                    .GetProperty("data")[0]
                    .GetProperty("url")
                    .GetString();

                if (string.IsNullOrEmpty(imageUrl))
                {
                    throw new Exception("Failed to get image URL from OpenAI API.");
                }

                // Download the image and save it locally
                var imageResponse = await _httpClient.GetAsync(imageUrl);
                imageResponse.EnsureSuccessStatusCode();

                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                var imageBase64 = Convert.ToBase64String(imageBytes);

                return imageBase64;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI DALL-E API");
                throw new Exception("Failed to generate exercise visualization image.", ex);
            }
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }
    }
}

