using System.Text;
using System.Text.Json;
using FitnessCenter.Constants;

namespace FitnessCenter.Services
{
    public interface IGeminiApiClient
    {
        Task<string> GenerateContentAsync(string prompt, Stream? photoStream = null, string? mimeType = null);
    }

    public class GeminiApiClient : IGeminiApiClient
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiApiClient> _logger;

        public GeminiApiClient(
            IConfiguration configuration, 
            IHttpClientFactory httpClientFactory, 
            ILogger<GeminiApiClient> logger)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        public async Task<string> GenerateContentAsync(string prompt, Stream? photoStream = null, string? mimeType = null)
        {
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

            _logger.LogInformation("Starting Google Gemini API call - Prompt length: {PromptLength}", prompt.Length);

            var apiKey = GetApiKey();
            var model = GetModel();
            ValidateApiKey(apiKey);

            try
            {
                var requestBody = BuildRequestBody(prompt, photoStream, mimeType);
                var response = await SendRequestAsync(apiKey, model, requestBody);
                return ExtractResponseText(response);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Google Gemini API");
                throw new Exception($"Failed to connect to Google Gemini API: {ex.Message}. Please check your API key and internet connection.", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error from Google Gemini API");
                throw new Exception("Failed to parse response from Google Gemini API. The API may have returned invalid JSON.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Google Gemini API");
                throw new Exception($"Failed to generate recommendations from AI service: {ex.Message}", ex);
            }
        }

        private string GetApiKey()
        {
            return Environment.GetEnvironmentVariable(AppConstants.GoogleGeminiApiKeyEnvVar, EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable(AppConstants.GoogleGeminiApiKeyEnvVar, EnvironmentVariableTarget.Machine)
                ?? Environment.GetEnvironmentVariable(AppConstants.GoogleGeminiApiKeyEnvVar)
                ?? _configuration[AppConstants.GoogleGeminiApiKeyConfig]
                ?? _configuration["GoogleGemini:ApiKey"] ?? string.Empty;
        }

        private string GetModel()
        {
            return _configuration[AppConstants.GoogleGeminiModelConfig] ?? AppConstants.DefaultGeminiModel;
        }

        private void ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Google Gemini API key is missing!");
                throw new InvalidOperationException(AppConstants.ErrorApiKeyMissing);
            }
        }

        private object BuildRequestBody(string prompt, Stream? photoStream, string? mimeType)
        {
            var parts = new List<object> { new { text = prompt } };

            if (photoStream != null && !string.IsNullOrEmpty(mimeType))
            {
                var imagePart = BuildImagePart(photoStream, mimeType);
                parts.Add(imagePart);
            }

            return new { contents = new[] { new { parts } } };
        }

        private object BuildImagePart(Stream photoStream, string mimeType)
        {
            _logger.LogInformation("Processing image - MIME Type: {MimeType}", mimeType);
            
            using var memoryStream = new MemoryStream();
            photoStream.CopyTo(memoryStream);
            var imageBytes = memoryStream.ToArray();
            var base64Image = Convert.ToBase64String(imageBytes);
            
            _logger.LogInformation("Image processed - Size: {Size} bytes, Base64 Length: {Base64Length}", 
                imageBytes.Length, base64Image.Length);

            return new
            {
                inline_data = new
                {
                    mime_type = mimeType,
                    data = base64Image
                }
            };
        }

        private async Task<HttpResponseMessage> SendRequestAsync(string apiKey, string model, object requestBody)
        {
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var url = BuildApiUrl(apiKey, model);
            var maskedUrl = url.Replace(apiKey, "***MASKED***");
            _logger.LogInformation("Sending request to: {Url}", maskedUrl);

            var response = await _httpClient.PostAsync(url, content);
            _logger.LogInformation("Response received - Status Code: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                response = await HandleErrorResponseAsync(response, url, apiKey, model, json);
            }

            return response;
        }

        private string BuildApiUrl(string apiKey, string model)
        {
            var path = string.Format(AppConstants.GeminiApiV1BetaPath, model);
            return $"{AppConstants.GeminiApiBaseUrl}{path}?key={apiKey}";
        }

        private async Task<HttpResponseMessage> HandleErrorResponseAsync(HttpResponseMessage response, string url, string apiKey, string model, string requestJson)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var maskedUrl = url.Replace(apiKey, "***MASKED***");
            
            _logger.LogError("Google Gemini API Error - Status: {StatusCode}, Response: {ErrorContent}", 
                response.StatusCode, errorContent);

            var detailedErrorMessage = ParseErrorResponse(errorContent, response.StatusCode);

            // Try v1 endpoint as fallback for 404 errors
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("404 Not Found - Trying v1 endpoint as fallback");
                var fallbackUrl = BuildFallbackUrl(apiKey, model);
                var fallbackResponse = await RetryWithFallbackUrlAsync(fallbackUrl, requestJson);
                
                if (!fallbackResponse.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Google Gemini API returned {response.StatusCode}: {detailedErrorMessage}");
                }
                
                return fallbackResponse;
            }

            throw new HttpRequestException($"Google Gemini API returned {response.StatusCode}: {detailedErrorMessage}");
        }

        private string BuildFallbackUrl(string apiKey, string model)
        {
            var path = string.Format(AppConstants.GeminiApiV1Path, model);
            return $"{AppConstants.GeminiApiBaseUrl}{path}?key={apiKey}";
        }

        private async Task<HttpResponseMessage> RetryWithFallbackUrlAsync(string url, string jsonContent)
        {
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var maskedUrl = url.Replace(url.Split('=').Last(), "***MASKED***");
            _logger.LogInformation("Retrying with fallback URL: {Url}", maskedUrl);
            
            var response = await _httpClient.PostAsync(url, content);
            _logger.LogInformation("Fallback response - Status Code: {StatusCode}", response.StatusCode);
            
            return response;
        }

        private string ParseErrorResponse(string errorContent, System.Net.HttpStatusCode statusCode)
        {
            try
            {
                var errorJson = JsonDocument.Parse(errorContent);
                if (errorJson.RootElement.TryGetProperty("error", out var errorObj))
                {
                    var message = errorObj.TryGetProperty("message", out var messageElement) 
                        ? messageElement.GetString() ?? string.Empty 
                        : string.Empty;
                    var status = errorObj.TryGetProperty("status", out var statusElement) 
                        ? statusElement.GetString() ?? string.Empty 
                        : string.Empty;

                    if (statusCode == System.Net.HttpStatusCode.Forbidden && 
                        message.Contains("leaked", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Your API key has been reported as leaked and is no longer valid. " +
                               "Please generate a new API key from Google AI Studio and update your configuration.";
                    }

                    if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return "Invalid API key. Please verify your GOOGLE_GEMINI_API_KEY contains a valid API key.";
                    }

                    if (statusCode == System.Net.HttpStatusCode.TooManyRequests || 
                        (statusCode == System.Net.HttpStatusCode.Forbidden && message.Contains("quota", StringComparison.OrdinalIgnoreCase)))
                    {
                        return "API quota exceeded. Please check your Google Cloud billing and quota limits.";
                    }

                    return !string.IsNullOrEmpty(message) ? $"API Error ({status}): {message}" : errorContent;
                }
            }
            catch (JsonException)
            {
                _logger.LogWarning("Could not parse error response as JSON, using raw content");
            }

            return errorContent;
        }

        private string ExtractResponseText(HttpResponseMessage response)
        {
            var responseContent = response.Content.ReadAsStringAsync().Result;
            _logger.LogInformation("Response content length: {Length} characters", responseContent.Length);

            var responseJson = JsonDocument.Parse(responseContent);

            if (!responseJson.RootElement.TryGetProperty("candidates", out var candidates) || 
                candidates.GetArrayLength() == 0)
            {
                _logger.LogError("No candidates in response - Full Response: {Response}", responseContent);
                throw new Exception("No response candidates from Google Gemini API. The API may have blocked the request or returned an error.");
            }

            _logger.LogInformation("Found {Count} candidate(s) in response", candidates.GetArrayLength());

            var text = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("Response text is null or empty");
                return "Unable to generate recommendations.";
            }

            _logger.LogInformation("Successfully received response - Length: {Length} characters", text.Length);
            return text;
        }
    }
}
