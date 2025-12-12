using FitnessCenter.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FitnessCenter.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Member>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Create roles
            string[] roles = { "Admin", "Member" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Create admin users
            var adminUsers = new[]
            {
                new { Email = "ogrencinumarasi@sakarya.edu.tr", Password = "sau", FirstName = "Admin", LastName = "User" },
                new { Email = "g201210589@sakarya.edu.tr", Password = "sau", FirstName = "Admin", LastName = "User" }
            };

            foreach (var adminUser in adminUsers)
            {
                if (await userManager.FindByEmailAsync(adminUser.Email) == null)
                {
                    var admin = new Member
                    {
                        UserName = adminUser.Email,
                        Email = adminUser.Email,
                        EmailConfirmed = true,
                        FirstName = adminUser.FirstName,
                        LastName = adminUser.LastName
                    };

                    var result = await userManager.CreateAsync(admin, adminUser.Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(admin, "Admin");
                    }
                }
            }

            // Seed initial data if database is empty
            if (!context.FitnessCenters.Any())
            {
                var fitnessCenter = new Models.FitnessCenter
                {
                    Name = "Sakarya Fitness Center",
                    Address = "Sakarya University Campus, Serdivan, Sakarya",
                    Phone = "+90 264 295 0000",
                    Email = "info@sakaryafitness.edu.tr",
                    CreatedDate = DateTime.Now
                };

                context.FitnessCenters.Add(fitnessCenter);
                await context.SaveChangesAsync();

                // Add working hours for fitness center (Monday to Friday, 6 AM - 10 PM)
                var centerWorkingHours = new List<WorkingHours>();
                for (int i = 1; i <= 5; i++) // Monday to Friday
                {
                    centerWorkingHours.Add(new WorkingHours
                    {
                        FitnessCenterId = fitnessCenter.Id,
                        DayOfWeek = (DayOfWeek)i,
                        StartTime = new TimeSpan(6, 0, 0),
                        EndTime = new TimeSpan(22, 0, 0)
                    });
                }
                // Saturday (6 AM - 8 PM)
                centerWorkingHours.Add(new WorkingHours
                {
                    FitnessCenterId = fitnessCenter.Id,
                    DayOfWeek = DayOfWeek.Saturday,
                    StartTime = new TimeSpan(6, 0, 0),
                    EndTime = new TimeSpan(20, 0, 0)
                });

                context.WorkingHours.AddRange(centerWorkingHours);

                // Add sample services
                var services = new List<Service>
                {
                    new Service
                    {
                        Name = "Personal Training",
                        Type = ServiceType.Fitness,
                        Description = "One-on-one personal training session with certified trainer",
                        Price = 500.00m,
                        DurationMinutes = 60,
                        FitnessCenterId = fitnessCenter.Id
                    },
                    new Service
                    {
                        Name = "Yoga Class",
                        Type = ServiceType.Yoga,
                        Description = "Group yoga session for all levels",
                        Price = 150.00m,
                        DurationMinutes = 60,
                        FitnessCenterId = fitnessCenter.Id
                    },
                    new Service
                    {
                        Name = "Pilates Class",
                        Type = ServiceType.Pilates,
                        Description = "Group pilates session focusing on core strength",
                        Price = 150.00m,
                        DurationMinutes = 45,
                        FitnessCenterId = fitnessCenter.Id
                    }
                };

                context.Services.AddRange(services);
                await context.SaveChangesAsync();

                // Add sample trainers
                var trainers = new List<Trainer>
                {
                    new Trainer
                    {
                        FirstName = "Ahmet",
                        LastName = "Yılmaz",
                        Email = "ahmet.yilmaz@sakaryafitness.edu.tr",
                        Phone = "+90 555 111 2233",
                        Bio = "Certified personal trainer with 10 years of experience",
                        FitnessCenterId = fitnessCenter.Id
                    },
                    new Trainer
                    {
                        FirstName = "Ayşe",
                        LastName = "Demir",
                        Email = "ayse.demir@sakaryafitness.edu.tr",
                        Phone = "+90 555 222 3344",
                        Bio = "Yoga instructor specializing in Hatha and Vinyasa yoga",
                        FitnessCenterId = fitnessCenter.Id
                    },
                    new Trainer
                    {
                        FirstName = "Mehmet",
                        LastName = "Kaya",
                        Email = "mehmet.kaya@sakaryafitness.edu.tr",
                        Phone = "+90 555 333 4455",
                        Bio = "Pilates instructor with expertise in rehabilitation",
                        FitnessCenterId = fitnessCenter.Id
                    }
                };

                context.Trainers.AddRange(trainers);
                await context.SaveChangesAsync();

                // Assign expertise to trainers
                var trainerExpertises = new List<TrainerExpertise>
                {
                    new TrainerExpertise { TrainerId = trainers[0].Id, ServiceId = services[0].Id }, // Ahmet - Personal Training
                    new TrainerExpertise { TrainerId = trainers[1].Id, ServiceId = services[1].Id }, // Ayşe - Yoga
                    new TrainerExpertise { TrainerId = trainers[2].Id, ServiceId = services[2].Id }  // Mehmet - Pilates
                };

                context.TrainerExpertises.AddRange(trainerExpertises);

                // Add working hours for trainers
                var trainerWorkingHours = new List<WorkingHours>();
                foreach (var trainer in trainers)
                {
                    for (int i = 1; i <= 5; i++) // Monday to Friday
                    {
                        trainerWorkingHours.Add(new WorkingHours
                        {
                            TrainerId = trainer.Id,
                            DayOfWeek = (DayOfWeek)i,
                            StartTime = new TimeSpan(9, 0, 0),
                            EndTime = new TimeSpan(18, 0, 0)
                        });
                    }
                }
                context.WorkingHours.AddRange(trainerWorkingHours);

                await context.SaveChangesAsync();
            }
        }
    }
}

