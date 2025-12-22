using FitnessCenter.Constants;

namespace FitnessCenter.Helpers
{
    public static class FileUploadHelper
    {
        public static bool IsValidImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return AppConstants.AllowedImageExtensions.Contains(fileExtension);
        }

        public static bool IsFileSizeValid(IFormFile file)
        {
            return file != null && file.Length <= AppConstants.MaxFileSizeBytes;
        }

        public static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => AppConstants.MimeTypeJpeg,
                ".png" => AppConstants.MimeTypePng,
                ".gif" => AppConstants.MimeTypeGif,
                ".webp" => AppConstants.MimeTypeWebp,
                _ => AppConstants.MimeTypeJpeg
            };
        }

        public static async Task<string> SaveFileAsync(
            IFormFile file, 
            string uploadsFolder, 
            string userId, 
            IWebHostEnvironment environment)
        {
            var uploadsPath = Path.Combine(environment.WebRootPath, uploadsFolder);
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/{uploadsFolder}/{uniqueFileName}";
        }
    }
}
