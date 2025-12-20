using FitnessCenter.Constants;

namespace FitnessCenter.Services
{
    public interface IFileValidationService
    {
        (bool IsValid, string? ErrorMessage) ValidateImageFile(IFormFile? file);
    }

    public class FileValidationService : IFileValidationService
    {
        public (bool IsValid, string? ErrorMessage) ValidateImageFile(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return (false, AppConstants.ErrorNoPhotoProvided);
            }

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AppConstants.AllowedImageExtensions.Contains(fileExtension))
            {
                return (false, AppConstants.ErrorInvalidFileType);
            }

            if (file.Length > AppConstants.MaxFileSizeBytes)
            {
                return (false, AppConstants.ErrorFileTooLarge);
            }

            return (true, null);
        }
    }
}
