using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Models
{
    public class WorkingHours
    {
        public int Id { get; set; }

        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        // Navigation properties
        public int? FitnessCenterId { get; set; }
        public FitnessCenter? FitnessCenter { get; set; }

        public int? TrainerId { get; set; }
        public Trainer? Trainer { get; set; }
    }
}

