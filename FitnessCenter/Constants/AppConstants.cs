namespace FitnessCenter.Constants
{
    public static class AppConstants
    {
        // File Upload Constants
        public const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
        public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        public const string UploadsFolder = "uploads";
        public const string AIPhotosFolder = "ai-photos";

        // API Configuration Keys
        public const string GoogleGeminiApiKeyEnvVar = "GOOGLE_GEMINI_API_KEY";
        public const string GoogleGeminiApiKeyConfig = "GOOGLE_GEMINI_API_KEY";
        public const string GoogleGeminiModelConfig = "GoogleGemini:Model";
        public const string DefaultGeminiModel = "gemini-1.5-flash";

        // API Endpoints
        public const string GeminiApiBaseUrl = "https://generativelanguage.googleapis.com";
        public const string GeminiApiV1BetaPath = "/v1beta/models/{0}:generateContent";
        public const string GeminiApiV1Path = "/v1/models/{0}:generateContent";

        // MIME Types
        public const string MimeTypeJpeg = "image/jpeg";
        public const string MimeTypePng = "image/png";
        public const string MimeTypeGif = "image/gif";
        public const string MimeTypeWebp = "image/webp";

        // Error Messages
        public const string ErrorNoPhotoProvided = "Please select a photo to upload.";
        public const string ErrorInvalidFileType = "Please upload a valid image file (JPG, PNG, or GIF).";
        public const string ErrorFileTooLarge = "File size must be less than 10MB.";
        public const string ErrorApiKeyMissing = "Google Gemini API key is not configured. Please set the GOOGLE_GEMINI_API_KEY environment variable or add it to appsettings.json.";
        public const string ErrorHeightWeightRequired = "Height and weight are required for diet plan recommendations.";
        public const string ErrorExerciseNameRequired = "Exercise name is required.";
        public const string ErrorTrainerNotAvailable = "The selected trainer is not available at this time.";
    }
}
