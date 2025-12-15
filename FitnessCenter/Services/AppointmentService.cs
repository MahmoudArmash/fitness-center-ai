using FitnessCenter.Data;
using FitnessCenter.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FitnessCenter.Services
{
    public interface IAppointmentService
    {
        Task<bool> IsTrainerAvailableAsync(int trainerId, DateTime appointmentDateTime, int durationMinutes, int? excludeAppointmentId = null);
        Task<bool> HasConflictAsync(int trainerId, DateTime appointmentDateTime, int durationMinutes, int? excludeAppointmentId = null);
        Task<List<Trainer>> GetAvailableTrainersAsync(DateTime date, int serviceId);
    }

    public class AppointmentService : IAppointmentService
    {
        private readonly ApplicationDbContext _context;

        public AppointmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsTrainerAvailableAsync(int trainerId, DateTime appointmentDateTime, int durationMinutes, int? excludeAppointmentId = null)
        {
            // Check if trainer has working hours for this day
            var dayOfWeek = appointmentDateTime.DayOfWeek;
            var trainerWorkingHours = await _context.WorkingHours
                .Where(wh => wh.TrainerId == trainerId && wh.DayOfWeek == dayOfWeek)
                .FirstOrDefaultAsync();

            if (trainerWorkingHours == null)
                return false;

            var appointmentStart = appointmentDateTime.TimeOfDay;
            var appointmentEnd = appointmentStart.Add(TimeSpan.FromMinutes(durationMinutes));

            // Check if appointment time is within working hours
            if (appointmentStart < trainerWorkingHours.StartTime || appointmentEnd > trainerWorkingHours.EndTime)
                return false;

            // Check for conflicts with existing appointments
            return !await HasConflictAsync(trainerId, appointmentDateTime, durationMinutes, excludeAppointmentId);
        }

        public async Task<bool> HasConflictAsync(int trainerId, DateTime appointmentDateTime, int durationMinutes, int? excludeAppointmentId = null)
        {
            var appointmentEnd = appointmentDateTime.AddMinutes(durationMinutes);

            // Optimized LINQ query with better conflict detection logic
            var conflictingAppointments = await _context.Appointments
                .Where(a => a.TrainerId == trainerId &&
                           a.Status != AppointmentStatus.Cancelled &&
                           (excludeAppointmentId == null || a.Id != excludeAppointmentId.Value) &&
                           // Simplified conflict detection: appointments overlap if one starts before the other ends
                           a.AppointmentDateTime < appointmentEnd &&
                           a.AppointmentDateTime.AddMinutes(a.DurationMinutes) > appointmentDateTime)
                .AnyAsync();

            return conflictingAppointments;
        }

        public async Task<List<Trainer>> GetAvailableTrainersAsync(DateTime date, int serviceId)
        {
            var dayOfWeek = date.DayOfWeek;
            var timeOfDay = date.TimeOfDay;

            // Get service first using LINQ
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null)
                return new List<Trainer>();

            var appointmentEnd = timeOfDay.Add(TimeSpan.FromMinutes(service.DurationMinutes));

            // Use LINQ to get all relevant data in one query
            var trainersWithExpertise = await _context.Trainers
                .Where(t => t.TrainerExpertises.Any(te => te.ServiceId == serviceId))
                .Include(t => t.WorkingHours)
                .Include(t => t.Appointments)
                .ToListAsync();

            // Use LINQ Where and SelectMany for filtering
            var availableTrainers = trainersWithExpertise
                .Where(trainer =>
                {
                    // Check if trainer has working hours for this day using LINQ
                    var workingHours = trainer.WorkingHours
                        .FirstOrDefault(wh => wh.DayOfWeek == dayOfWeek);
                    
                    if (workingHours == null)
                        return false;

                    // Check if the requested time is within working hours
                    if (timeOfDay < workingHours.StartTime || appointmentEnd > workingHours.EndTime)
                        return false;

                    // Check for conflicts using LINQ
                    var appointmentEndDateTime = date.AddMinutes(service.DurationMinutes);
                    var hasConflict = trainer.Appointments
                        .Any(a => a.Status != AppointmentStatus.Cancelled &&
                                 a.AppointmentDateTime < appointmentEndDateTime &&
                                 a.AppointmentDateTime.AddMinutes(a.DurationMinutes) > date);

                    return !hasConflict;
                })
                .OrderBy(t => t.FullName)
                .ToList();

            return availableTrainers;
        }
    }
}

