using FitnessCenter.Data;
using FitnessCenter.Models;
using Microsoft.EntityFrameworkCore;

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

            var conflictingAppointments = await _context.Appointments
                .Where(a => a.TrainerId == trainerId &&
                           a.Status != AppointmentStatus.Cancelled &&
                           a.Id != (excludeAppointmentId ?? -1) &&
                           ((a.AppointmentDateTime <= appointmentDateTime && a.AppointmentDateTime.AddMinutes(a.DurationMinutes) > appointmentDateTime) ||
                            (a.AppointmentDateTime < appointmentEnd && a.AppointmentDateTime.AddMinutes(a.DurationMinutes) >= appointmentEnd) ||
                            (a.AppointmentDateTime >= appointmentDateTime && a.AppointmentDateTime.AddMinutes(a.DurationMinutes) <= appointmentEnd)))
                .AnyAsync();

            return conflictingAppointments;
        }

        public async Task<List<Trainer>> GetAvailableTrainersAsync(DateTime date, int serviceId)
        {
            var dayOfWeek = date.DayOfWeek;
            var timeOfDay = date.TimeOfDay;

            // Get trainers who have expertise in this service
            var trainersWithExpertise = await _context.Trainers
                .Where(t => t.TrainerExpertises.Any(te => te.ServiceId == serviceId))
                .Include(t => t.WorkingHours)
                .Include(t => t.Appointments)
                .ToListAsync();

            var availableTrainers = new List<Trainer>();

            foreach (var trainer in trainersWithExpertise)
            {
                // Check if trainer has working hours for this day
                var workingHours = trainer.WorkingHours.FirstOrDefault(wh => wh.DayOfWeek == dayOfWeek);
                if (workingHours == null)
                    continue;

                // Check if the requested time is within working hours
                var service = await _context.Services.FindAsync(serviceId);
                if (service == null)
                    continue;

                var appointmentEnd = timeOfDay.Add(TimeSpan.FromMinutes(service.DurationMinutes));
                if (timeOfDay < workingHours.StartTime || appointmentEnd > workingHours.EndTime)
                    continue;

                // Check for conflicts
                var hasConflict = await HasConflictAsync(trainer.Id, date, service.DurationMinutes);
                if (!hasConflict)
                {
                    availableTrainers.Add(trainer);
                }
            }

            return availableTrainers;
        }
    }
}

