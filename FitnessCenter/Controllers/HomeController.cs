using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Models;
using FitnessCenter.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace FitnessCenter.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<Member> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<Member> userManager, RoleManager<IdentityRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet]
    public async Task<IActionResult> DatabaseHealth()
    {
        try
        {
            // Test database connection
            var canConnect = await _context.Database.CanConnectAsync();
            
            // Get counts from database
            var fitnessCenterCount = await _context.FitnessCenters.CountAsync();
            var memberCount = await _context.Users.CountAsync();
            var serviceCount = await _context.Services.CountAsync();
            var trainerCount = await _context.Trainers.CountAsync();
            var appointmentCount = await _context.Appointments.CountAsync();

            var healthInfo = new
            {
                Status = "Healthy",
                CanConnect = canConnect,
                DatabaseFile = "FitnessCenter.db",
                DatabaseFileExists = System.IO.File.Exists("FitnessCenter.db"),
                DatabaseFileSize = System.IO.File.Exists("FitnessCenter.db") 
                    ? new FileInfo("FitnessCenter.db").Length 
                    : 0,
                Counts = new
                {
                    FitnessCenters = fitnessCenterCount,
                    Members = memberCount,
                    Services = serviceCount,
                    Trainers = trainerCount,
                    Appointments = appointmentCount
                },
                Timestamp = DateTime.Now
            };

            return Json(healthInfo);
        }
        catch (Exception ex)
        {
            return Json(new
            {
                Status = "Error",
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                Timestamp = DateTime.Now
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CreateAdmin()
    {
        try
        {
            // Ensure Admin role exists
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            const string adminEmail = "g201210589@sakarya.edu.tr";
            const string adminPassword = "sau";

            var existingUser = await _userManager.FindByEmailAsync(adminEmail);
            if (existingUser != null)
            {
                // User already exists, check if they're admin
                var isAdmin = await _userManager.IsInRoleAsync(existingUser, "Admin");
                if (isAdmin)
                {
                    return Json(new
                    {
                        Success = true,
                        Message = $"Admin user '{adminEmail}' already exists and is an admin.",
                        Timestamp = DateTime.Now
                    });
                }
                else
                {
                    // Add admin role
                    await _userManager.AddToRoleAsync(existingUser, "Admin");
                    return Json(new
                    {
                        Success = true,
                        Message = $"User '{adminEmail}' already exists. Admin role has been added.",
                        Timestamp = DateTime.Now
                    });
                }
            }

            // Create new admin user
            var admin = new Member
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Admin",
                LastName = "User"
            };

            var result = await _userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, "Admin");
                return Json(new
                {
                    Success = true,
                    Message = $"Admin user '{adminEmail}' created successfully!",
                    Email = adminEmail,
                    Password = adminPassword,
                    Timestamp = DateTime.Now
                });
            }
            else
            {
                return Json(new
                {
                    Success = false,
                    Message = "Failed to create admin user.",
                    Errors = result.Errors.Select(e => e.Description).ToArray(),
                    Timestamp = DateTime.Now
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new
            {
                Success = false,
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                Timestamp = DateTime.Now
            });
        }
    }
}
