using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessCenter.Models
{
    public class Member : IdentityUser
    {
        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        // Body metrics
        public decimal? Height { get; set; } // in cm
        public decimal? Weight { get; set; } // in kg

        public string? PhotoPath { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}

