using FitnessCenter.Models;
using Microsoft.AspNetCore.Identity;

namespace FitnessCenter.Services
{
    public class AdminPasswordValidator : IPasswordValidator<Member>
    {
        // Admin email addresses that can use "sau" password
        private static readonly HashSet<string> AdminEmails = new()
        {
            "ogrencinumarasi@sakarya.edu.tr",
            "g201210589@sakarya.edu.tr"
        };

        public Task<IdentityResult> ValidateAsync(UserManager<Member> manager, Member user, string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return Task.FromResult(IdentityResult.Failed(new IdentityError { Description = "Password cannot be empty." }));
            }

            // Check if this is an admin user by email pattern
            var isAdmin = !string.IsNullOrEmpty(user.Email) && AdminEmails.Contains(user.Email);
            
            // If admin and password is "sau", allow it without any validations
            if (isAdmin && password == "sau")
            {
                return Task.FromResult(IdentityResult.Success);
            }

            // For all other cases (regular users), apply standard validations
            var errors = new List<IdentityError>();

            if (password.Length < 6)
            {
                errors.Add(new IdentityError { Description = "Password must be at least 6 characters long." });
            }

            if (!password.Any(char.IsDigit))
            {
                errors.Add(new IdentityError { Description = "Password must contain at least one digit." });
            }

            if (!password.Any(char.IsLower))
            {
                errors.Add(new IdentityError { Description = "Password must contain at least one lowercase letter." });
            }

            if (!password.Any(char.IsUpper))
            {
                errors.Add(new IdentityError { Description = "Password must contain at least one uppercase letter." });
            }

            return Task.FromResult(errors.Count > 0 
                ? IdentityResult.Failed(errors.ToArray()) 
                : IdentityResult.Success);
        }
    }
}

