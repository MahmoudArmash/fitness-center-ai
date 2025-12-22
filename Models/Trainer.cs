using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessCenter.Models
{
    public class Trainer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        public int FitnessCenterId { get; set; }
        public FitnessCenter FitnessCenter { get; set; } = null!;

        // Navigation properties
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<TrainerExpertise> TrainerExpertises { get; set; } = new List<TrainerExpertise>();
        public ICollection<WorkingHours> WorkingHours { get; set; } = new List<WorkingHours>();

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}

