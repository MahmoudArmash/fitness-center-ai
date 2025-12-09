using FitnessCenter.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FitnessCenter.Data
{
    public class ApplicationDbContext : IdentityDbContext<Member>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<FitnessCenter> FitnessCenters { get; set; }
        public DbSet<Trainer> Trainers { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<WorkingHours> WorkingHours { get; set; }
        public DbSet<TrainerExpertise> TrainerExpertises { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure TrainerExpertise as a many-to-many relationship
            builder.Entity<TrainerExpertise>()
                .HasKey(te => new { te.TrainerId, te.ServiceId });

            builder.Entity<TrainerExpertise>()
                .HasOne(te => te.Trainer)
                .WithMany(t => t.TrainerExpertises)
                .HasForeignKey(te => te.TrainerId);

            builder.Entity<TrainerExpertise>()
                .HasOne(te => te.Service)
                .WithMany(s => s.TrainerExpertises)
                .HasForeignKey(te => te.ServiceId);

            // Configure WorkingHours - can belong to either FitnessCenter or Trainer
            builder.Entity<WorkingHours>()
                .HasOne(wh => wh.FitnessCenter)
                .WithMany(fc => fc.WorkingHours)
                .HasForeignKey(wh => wh.FitnessCenterId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorkingHours>()
                .HasOne(wh => wh.Trainer)
                .WithMany(t => t.WorkingHours)
                .HasForeignKey(wh => wh.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationships
            builder.Entity<Service>()
                .HasOne(s => s.FitnessCenter)
                .WithMany(fc => fc.Services)
                .HasForeignKey(s => s.FitnessCenterId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Trainer>()
                .HasOne(t => t.FitnessCenter)
                .WithMany(fc => fc.Trainers)
                .HasForeignKey(t => t.FitnessCenterId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Appointment>()
                .HasOne(a => a.Member)
                .WithMany(m => m.Appointments)
                .HasForeignKey(a => a.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Trainer)
                .WithMany(t => t.Appointments)
                .HasForeignKey(a => a.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Service)
                .WithMany(s => s.Appointments)
                .HasForeignKey(a => a.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

