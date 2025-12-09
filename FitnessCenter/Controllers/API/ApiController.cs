using FitnessCenter.Data;
using FitnessCenter.Models;
using FitnessCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessCenter.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppointmentService _appointmentService;
        private readonly ILogger<ApiController> _logger;

        public ApiController(
            ApplicationDbContext context,
            IAppointmentService appointmentService,
            ILogger<ApiController> logger)
        {
            _context = context;
            _appointmentService = appointmentService;
            _logger = logger;
        }

        [HttpGet("trainers")]
        public async Task<ActionResult<IEnumerable<object>>> GetTrainers()
        {
            var trainers = await _context.Trainers
                .Include(t => t.FitnessCenter)
                .Include(t => t.TrainerExpertises)
                    .ThenInclude(te => te.Service)
                .Select(t => new
                {
                    t.Id,
                    t.FirstName,
                    t.LastName,
                    t.FullName,
                    t.Email,
                    t.Phone,
                    t.Bio,
                    FitnessCenter = new { t.FitnessCenter.Id, t.FitnessCenter.Name },
                    Services = t.TrainerExpertises.Select(te => new { te.Service.Id, te.Service.Name, te.Service.Type })
                })
                .ToListAsync();

            return Ok(trainers);
        }

        [HttpGet("trainers/available")]
        public async Task<ActionResult<IEnumerable<object>>> GetAvailableTrainers([FromQuery] DateTime date, [FromQuery] int serviceId)
        {
            if (date == default)
            {
                return BadRequest("Date parameter is required.");
            }

            var trainers = await _appointmentService.GetAvailableTrainersAsync(date, serviceId);

            var result = trainers.Select(t => new
            {
                t.Id,
                t.FirstName,
                t.LastName,
                t.FullName,
                t.Email,
                t.Phone,
                t.Bio
            });

            return Ok(result);
        }

        [HttpGet("appointments/member/{memberId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetMemberAppointments(string memberId)
        {
            var appointments = await _context.Appointments
                .Where(a => a.MemberId == memberId)
                .Include(a => a.Member)
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .OrderByDescending(a => a.AppointmentDateTime)
                .Select(a => new
                {
                    a.Id,
                    a.AppointmentDateTime,
                    a.DurationMinutes,
                    a.Price,
                    a.Status,
                    a.Notes,
                    Member = new { a.Member.Id, a.Member.FullName, a.Member.Email },
                    Trainer = new { a.Trainer.Id, a.Trainer.FullName },
                    Service = new { a.Service.Id, a.Service.Name, a.Service.Type }
                })
                .ToListAsync();

            return Ok(appointments);
        }

        [HttpGet("appointments/trainer/{trainerId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetTrainerAppointments(int trainerId)
        {
            var appointments = await _context.Appointments
                .Where(a => a.TrainerId == trainerId)
                .Include(a => a.Member)
                .Include(a => a.Service)
                .OrderByDescending(a => a.AppointmentDateTime)
                .Select(a => new
                {
                    a.Id,
                    a.AppointmentDateTime,
                    a.DurationMinutes,
                    a.Price,
                    a.Status,
                    a.Notes,
                    Member = new { a.Member.Id, a.Member.FullName, a.Member.Email },
                    Service = new { a.Service.Id, a.Service.Name, a.Service.Type }
                })
                .ToListAsync();

            return Ok(appointments);
        }

        [HttpGet("services")]
        public async Task<ActionResult<IEnumerable<object>>> GetServices()
        {
            var services = await _context.Services
                .Include(s => s.FitnessCenter)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Type,
                    s.Description,
                    s.Price,
                    s.DurationMinutes,
                    FitnessCenter = new { s.FitnessCenter.Id, s.FitnessCenter.Name }
                })
                .ToListAsync();

            return Ok(services);
        }
    }
}

