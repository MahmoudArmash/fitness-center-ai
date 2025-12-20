using FitnessCenter.DTOs;
using FitnessCenter.Models;

namespace FitnessCenter.Helpers
{
    public static class BodyMetricsHelper
    {
        public static BodyMetricsDto GetFinalMetrics(
            decimal? formHeight,
            decimal? formWeight,
            string? formGender,
            DateTime? formDateOfBirth,
            Member? user,
            bool useProfileMetrics)
        {
            var metrics = new BodyMetricsDto
            {
                Height = formHeight,
                Weight = formWeight,
                Gender = formGender,
                DateOfBirth = formDateOfBirth
            };

            if (useProfileMetrics && user != null)
            {
                metrics.Height ??= user.Height;
                metrics.Weight ??= user.Weight;
                metrics.Gender ??= user.Gender;
                metrics.DateOfBirth ??= user.DateOfBirth;
            }

            return metrics;
        }
    }
}
