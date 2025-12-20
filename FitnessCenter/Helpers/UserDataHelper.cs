using FitnessCenter.Models;
using Microsoft.AspNetCore.Identity;

namespace FitnessCenter.Helpers
{
    public static class UserDataHelper
    {
        public static void SetUserViewBagData(dynamic viewBag, Member? user)
        {
            if (user == null) return;

            viewBag.UserHeight = user.Height;
            viewBag.UserWeight = user.Weight;
            viewBag.UserGender = user.Gender;
            viewBag.UserDateOfBirth = user.DateOfBirth;
            viewBag.UserPhotoPath = user.PhotoPath;
        }

        public static async Task<Member?> GetCurrentUserAsync(UserManager<Member> userManager, System.Security.Claims.ClaimsPrincipal user)
        {
            return await userManager.GetUserAsync(user);
        }
    }
}
