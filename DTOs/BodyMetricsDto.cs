namespace FitnessCenter.DTOs
{
    public class BodyMetricsDto
    {
        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public int? Age => DateOfBirth.HasValue 
            ? (int)((DateTime.Now - DateOfBirth.Value).TotalDays / 365.25) 
            : null;

        public bool HasAnyMetrics => Height.HasValue || Weight.HasValue || 
                                     !string.IsNullOrEmpty(Gender) || DateOfBirth.HasValue;
    }
}
