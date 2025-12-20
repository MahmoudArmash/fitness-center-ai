using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.ViewModels
{
    public class TrainerWorkingHoursViewModel
    {
        public int? Id { get; set; }
        
        [Required]
        [Display(Name = "Day of Week")]
        public DayOfWeek DayOfWeek { get; set; }
        
        [Required]
        [Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }
        
        [Required]
        [Display(Name = "End Time")]
        [DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; }
    }
}
