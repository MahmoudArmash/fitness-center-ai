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

            // Create admin users with password "sau" (no validations)
            var adminUsers = new[]
            {
                new { Email = "ogrencinumarasi@sakarya.edu.tr", Password = "sau", FirstName = "Admin", LastName = "User" },
                new { Email = "g201210589@sakarya.edu.tr", Password = "sau", FirstName = "Admin", LastName = "User" }
            };

            foreach (var adminUser in adminUsers)
            {
                var existingUser = await userManager.FindByEmailAsync(adminUser.Email);
                if (existingUser == null)
                {
                    var admin = new Member
                    {
                        UserName = adminUser.Email,
                        Email = adminUser.Email,
                        EmailConfirmed = true,
                        FirstName = adminUser.FirstName,
                        LastName = adminUser.LastName,
                        CreatedDate = DateTime.Now
                    };

                    // Create admin user first
                    var result = await userManager.CreateAsync(admin, adminUser.Password);
                    if (result.Succeeded)
                    {
                        // Add admin role - this allows "sau" password to work
                        await userManager.AddToRoleAsync(admin, "Admin");
                    }
                }
                else
                {
                    // Ensure existing user has admin role
                    var isAdmin = await userManager.IsInRoleAsync(existingUser, "Admin");
                    if (!isAdmin)
                    {
                        await userManager.AddToRoleAsync(existingUser, "Admin");
                    }
                    
                    // Reset password to "sau" for admin users (will work because they're admin)
                    var token = await userManager.GeneratePasswordResetTokenAsync(existingUser);
                    await userManager.ResetPasswordAsync(existingUser, token, adminUser.Password);
                }
            }

            // Ensure fitness center exists
            var fitnessCenter = await context.FitnessCenters.FirstOrDefaultAsync(fc => fc.Name == "Sakarya Fitness Center");
            if (fitnessCenter == null)
            {
                fitnessCenter = new Models.FitnessCenter
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
                await context.SaveChangesAsync();
            }

            // Ensure services exist
            var services = new List<Service>();
            var personalTraining = await context.Services.FirstOrDefaultAsync(s => s.Name == "Personal Training");
            if (personalTraining == null)
            {
                personalTraining = new Service
                {
                    Name = "Personal Training",
                    Type = ServiceType.Fitness,
                    Description = "One-on-one personal training session with certified trainer",
                    Price = 500.00m,
                    DurationMinutes = 60,
                    FitnessCenterId = fitnessCenter.Id
                };
                context.Services.Add(personalTraining);
            }
            services.Add(personalTraining);

            var yogaClass = await context.Services.FirstOrDefaultAsync(s => s.Name == "Yoga Class");
            if (yogaClass == null)
            {
                yogaClass = new Service
                {
                    Name = "Yoga Class",
                    Type = ServiceType.Yoga,
                    Description = "Group yoga session for all levels",
                    Price = 150.00m,
                    DurationMinutes = 60,
                    FitnessCenterId = fitnessCenter.Id
                };
                context.Services.Add(yogaClass);
            }
            services.Add(yogaClass);

            var pilatesClass = await context.Services.FirstOrDefaultAsync(s => s.Name == "Pilates Class");
            if (pilatesClass == null)
            {
                pilatesClass = new Service
                {
                    Name = "Pilates Class",
                    Type = ServiceType.Pilates,
                    Description = "Group pilates session focusing on core strength",
                    Price = 150.00m,
                    DurationMinutes = 45,
                    FitnessCenterId = fitnessCenter.Id
                };
                context.Services.Add(pilatesClass);
            }
            services.Add(pilatesClass);
            await context.SaveChangesAsync();

            // Ensure trainers exist - recreate if missing
            var trainers = new List<Trainer>();
            
            var ahmet = await context.Trainers.FirstOrDefaultAsync(t => t.Email == "ahmet.yilmaz@sakaryafitness.edu.tr");
            if (ahmet == null)
            {
                ahmet = new Trainer
                {
                    FirstName = "Ahmet",
                    LastName = "Yılmaz",
                    Email = "ahmet.yilmaz@sakaryafitness.edu.tr",
                    Phone = "+90 555 111 2233",
                    Bio = "Certified personal trainer with 10 years of experience",
                    FitnessCenterId = fitnessCenter.Id
                };
                context.Trainers.Add(ahmet);
            }
            else
            {
                // Update existing trainer info
                ahmet.FirstName = "Ahmet";
                ahmet.LastName = "Yılmaz";
                ahmet.Phone = "+90 555 111 2233";
                ahmet.Bio = "Certified personal trainer with 10 years of experience";
                ahmet.FitnessCenterId = fitnessCenter.Id;
            }
            trainers.Add(ahmet);

            var ayse = await context.Trainers.FirstOrDefaultAsync(t => t.Email == "ayse.demir@sakaryafitness.edu.tr");
            if (ayse == null)
            {
                ayse = new Trainer
                {
                    FirstName = "Ayşe",
                    LastName = "Demir",
                    Email = "ayse.demir@sakaryafitness.edu.tr",
                    Phone = "+90 555 222 3344",
                    Bio = "Yoga instructor specializing in Hatha and Vinyasa yoga",
                    FitnessCenterId = fitnessCenter.Id
                };
                context.Trainers.Add(ayse);
            }
            else
            {
                ayse.FirstName = "Ayşe";
                ayse.LastName = "Demir";
                ayse.Phone = "+90 555 222 3344";
                ayse.Bio = "Yoga instructor specializing in Hatha and Vinyasa yoga";
                ayse.FitnessCenterId = fitnessCenter.Id;
            }
            trainers.Add(ayse);

            var mehmet = await context.Trainers.FirstOrDefaultAsync(t => t.Email == "mehmet.kaya@sakaryafitness.edu.tr");
            if (mehmet == null)
            {
                mehmet = new Trainer
                {
                    FirstName = "Mehmet",
                    LastName = "Kaya",
                    Email = "mehmet.kaya@sakaryafitness.edu.tr",
                    Phone = "+90 555 333 4455",
                    Bio = "Pilates instructor with expertise in rehabilitation",
                    FitnessCenterId = fitnessCenter.Id
                };
                context.Trainers.Add(mehmet);
            }
            else
            {
                mehmet.FirstName = "Mehmet";
                mehmet.LastName = "Kaya";
                mehmet.Phone = "+90 555 333 4455";
                mehmet.Bio = "Pilates instructor with expertise in rehabilitation";
                mehmet.FitnessCenterId = fitnessCenter.Id;
            }
            trainers.Add(mehmet);

            await context.SaveChangesAsync();

            // Remove existing expertise and working hours for these trainers, then recreate
            foreach (var trainer in trainers)
            {
                // Remove existing expertise
                var existingExpertise = await context.TrainerExpertises
                    .Where(te => te.TrainerId == trainer.Id)
                    .ToListAsync();
                context.TrainerExpertises.RemoveRange(existingExpertise);

                // Remove existing working hours
                var existingWorkingHours = await context.WorkingHours
                    .Where(wh => wh.TrainerId == trainer.Id)
                    .ToListAsync();
                context.WorkingHours.RemoveRange(existingWorkingHours);
            }
            await context.SaveChangesAsync();

            // Assign expertise to trainers
            var trainerExpertises = new List<TrainerExpertise>
            {
                new TrainerExpertise { TrainerId = trainers[0].Id, ServiceId = services[0].Id }, // Ahmet - Personal Training
                new TrainerExpertise { TrainerId = trainers[1].Id, ServiceId = services[1].Id }, // Ayşe - Yoga
                new TrainerExpertise { TrainerId = trainers[2].Id, ServiceId = services[2].Id }  // Mehmet - Pilates
            };

            context.TrainerExpertises.AddRange(trainerExpertises);

            // Add working hours for trainers (Monday to Friday, 9 AM - 6 PM)
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

