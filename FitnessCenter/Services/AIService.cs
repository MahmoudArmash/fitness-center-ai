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
            _logger.LogInformation("=== Starting Google Gemini API Call ===");
            
            // Check environment variable first (more secure), then fall back to configuration
            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_GEMINI_API_KEY") 
                        ?? _configuration["GoogleGemini:ApiKey"];
            var model = _configuration["GoogleGemini:Model"] ?? "gemini-1.5-flash";

            var apiKeySource = Environment.GetEnvironmentVariable("GOOGLE_GEMINI_API_KEY") != null 
                ? "Environment Variable" 
                : "Configuration File";
            
            _logger.LogInformation("Configuration - Model: {Model}, API Key Present: {HasApiKey}, Source: {Source}, Prompt Length: {PromptLength}",
                model, !string.IsNullOrEmpty(apiKey), apiKeySource, prompt?.Length ?? 0);

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Google Gemini API key is missing!");
                throw new InvalidOperationException(
                    "Google Gemini API key is not configured. " +
                    "Please set the GOOGLE_GEMINI_API_KEY environment variable or add it to appsettings.json. " +
                    "For Windows: set GOOGLE_GEMINI_API_KEY=your-api-key " +
                    "For Linux/Mac: export GOOGLE_GEMINI_API_KEY=your-api-key");
            }

            try
            {
                var parts = new List<object>
                {
                    new { text = prompt }
                };

                // Add image if provided
                bool hasImage = false;
                if (photoStream != null && !string.IsNullOrEmpty(mimeType))
                {
                    _logger.LogInformation("Processing image - MIME Type: {MimeType}", mimeType);
                    using var memoryStream = new MemoryStream();
                    await photoStream.CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();
                    var base64Image = Convert.ToBase64String(imageBytes);
                    _logger.LogInformation("Image processed - Size: {Size} bytes, Base64 Length: {Base64Length}", 
                        imageBytes.Length, base64Image.Length);

                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = mimeType,
                            data = base64Image
                        }
                    });
                    hasImage = true;
                }
                else
                {
                    _logger.LogInformation("No image provided for this request");
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
                _logger.LogInformation("Request body created - JSON Length: {JsonLength}, Has Image: {HasImage}", 
                    json.Length, hasImage);

                _httpClient.DefaultRequestHeaders.Clear();
                // Use v1beta endpoint for Gemini models (this is the correct endpoint)
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                
                // Mask API key in logs for security
                var maskedUrl = url.Replace(apiKey, "***MASKED***");
                _logger.LogInformation("Sending request to: {Url}", maskedUrl);

                var response = await _httpClient.PostAsync(url, content);
                _logger.LogInformation("Response received - Status Code: {StatusCode}, Reason Phrase: {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("=== Google Gemini API ERROR ===");
                    _logger.LogError("Status Code: {StatusCode}", response.StatusCode);
                    _logger.LogError("Reason Phrase: {ReasonPhrase}", response.ReasonPhrase);
                    _logger.LogError("Error Response: {ErrorContent}", errorContent);
                    _logger.LogError("Request URL: {Url}", maskedUrl);
                    _logger.LogError("Model: {Model}, Has Image: {HasImage}", model, hasImage);
                    
                    // Try v1 endpoint as fallback
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("404 Not Found - Trying v1 endpoint as fallback...");
                        url = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";
                        maskedUrl = url.Replace(apiKey, "***MASKED***");
                        _logger.LogInformation("Retrying with URL: {Url}", maskedUrl);
                        
                        response = await _httpClient.PostAsync(url, content);
                        _logger.LogInformation("Fallback response - Status Code: {StatusCode}, Reason Phrase: {ReasonPhrase}", 
                            response.StatusCode, response.ReasonPhrase);
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogError("=== Fallback Request Also Failed ===");
                            _logger.LogError("Status Code: {StatusCode}", response.StatusCode);
                            _logger.LogError("Error Response: {ErrorContent}", errorContent);
                            throw new HttpRequestException($"Google Gemini API returned {response.StatusCode}: {errorContent}");
                        }
                        else
                        {
                            _logger.LogInformation("Fallback request succeeded!");
                        }
                    }
                    else
                    {
                        throw new HttpRequestException($"Google Gemini API returned {response.StatusCode}: {errorContent}");
                    }
                }
                else
                {
                    _logger.LogInformation("Request succeeded with status {StatusCode}", response.StatusCode);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Response content length: {Length} characters", responseContent.Length);
                
                var responseJson = JsonDocument.Parse(responseContent);

                // Check if response has candidates
                if (!responseJson.RootElement.TryGetProperty("candidates", out var candidates) || 
                    candidates.GetArrayLength() == 0)
                {
                    _logger.LogError("=== No Candidates in Response ===");
                    _logger.LogError("Full Response: {Response}", responseContent);
                    
                    // Check for error in response
                    if (responseJson.RootElement.TryGetProperty("error", out var error))
                    {
                        _logger.LogError("API Error Object: {Error}", error.ToString());
                    }
                    
                    throw new Exception("No response candidates from Google Gemini API. The API may have blocked the request or returned an error.");
                }

                _logger.LogInformation("Found {Count} candidate(s) in response", candidates.GetArrayLength());
                
                var recommendations = candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrEmpty(recommendations))
                {
                    _logger.LogWarning("Recommendations text is null or empty");
                }
                else
                {
                    _logger.LogInformation("Successfully received recommendations - Length: {Length} characters", recommendations.Length);
                }

                _logger.LogInformation("=== Google Gemini API Call Completed Successfully ===");
                return recommendations ?? "Unable to generate recommendations.";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "=== HTTP ERROR calling Google Gemini API ===");
                _logger.LogError("Exception Type: {ExceptionType}", ex.GetType().Name);
                _logger.LogError("Exception Message: {Message}", ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner Exception: {InnerException}", ex.InnerException.Message);
                }
                throw new Exception($"Failed to connect to Google Gemini API. Status: {ex.Message}. Please check your API key and internet connection.", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "=== JSON PARSING ERROR ===");
                _logger.LogError("Failed to parse response from Google Gemini API");
                throw new Exception("Failed to parse response from Google Gemini API. The API may have returned invalid JSON.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== UNEXPECTED ERROR calling Google Gemini API ===");
                _logger.LogError("Exception Type: {ExceptionType}", ex.GetType().Name);
                _logger.LogError("Exception Message: {Message}", ex.Message);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner Exception: {InnerException}", ex.InnerException.ToString());
                }
                throw new Exception($"Failed to generate recommendations from AI service: {ex.Message}", ex);
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
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to analyze user photo with Gemini, continuing with body metrics only");
                        // Continue without photo analysis
                    }
                }

                // Build the prompt for detailed visualization description using Gemini
                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine($"Create a detailed, vivid visualization description of a person performing the exercise '{exerciseName}'. ");
                promptBuilder.AppendLine("Provide a comprehensive description that includes:");

                // Use photo analysis if available, otherwise use body metrics
                if (!string.IsNullOrEmpty(bodyDescription))
                {
                    promptBuilder.AppendLine($"\nThe person has the following characteristics: {bodyDescription}");
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
                        promptBuilder.AppendLine($"\nThe person has a {bodyType} body type (height: {height}cm, weight: {weight}kg).");
                    }
                }

                if (!string.IsNullOrEmpty(gender))
                {
                    promptBuilder.AppendLine($"The person is {gender.ToLower()}.");
                }

                promptBuilder.AppendLine("\nProvide a detailed visualization description that includes:");
                promptBuilder.AppendLine("- Body position and posture during the exercise");
                promptBuilder.AppendLine("- Muscle groups being engaged (visible muscle definition)");
                promptBuilder.AppendLine("- Proper form and technique details");
                promptBuilder.AppendLine("- Facial expression and body language (showing focus and determination)");
                promptBuilder.AppendLine("- Workout attire and appearance");
                promptBuilder.AppendLine("- Setting and environment (professional fitness studio, clean background)");
                promptBuilder.AppendLine("- Lighting and overall aesthetic");
                promptBuilder.AppendLine("\nFormat the description in a clear, structured way with sections. Make it vivid and inspiring, as if describing a professional fitness photograph.");

                var prompt = promptBuilder.ToString();

                // Use Gemini to generate the detailed visualization description
                var visualizationDescription = await CallGeminiAPIAsync(prompt);

                return visualizationDescription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Google Gemini API for exercise visualization");
                throw new Exception("Failed to generate exercise visualization using Google AI.", ex);
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

