# Fitness Center Management System

A comprehensive ASP.NET Core MVC application for managing a fitness center with trainers, members, appointments, and AI-powered exercise recommendations.

## Features

- **User Authentication & Authorization**
  - Member registration and login
  - Role-based access control (Admin, Member)
  - Admin user: `ogrencinumarasi@sakarya.edu.tr` / `sau`

- **Fitness Center Management**
  - CRUD operations for Fitness Centers, Services, and Trainers (Admin only)
  - Working hours management
  - Trainer expertise assignment

- **Appointment System**
  - Book appointments with trainers
  - Conflict detection and availability checking
  - Appointment status management (Pending, Confirmed, Completed, Cancelled)

- **Member Features**
  - Profile management
  - Photo upload
  - View appointments
  - Body metrics tracking

- **AI Integration**
  - Photo-based exercise recommendations using Google Gemini AI
  - Personalized fitness plans with body metrics integration
  - Diet and nutrition plan recommendations

- **REST API**
  - `/api/api/trainers` - List all trainers
  - `/api/api/trainers/available?date={date}&serviceId={id}` - Available trainers
  - `/api/api/appointments/member/{memberId}` - Member appointments
  - `/api/api/appointments/trainer/{trainerId}` - Trainer appointments
  - `/api/api/services` - List all services

## Technology Stack

- **Framework**: ASP.NET Core MVC 9.0
- **Database**: SQLite with Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **Frontend**: Bootstrap 5, jQuery, jQuery Validation
- **AI**: Google Gemini AI (Gemini 1.5 Flash)

## Prerequisites

- .NET 9.0 SDK
- Google Gemini API Key (for AI features) - Get from [Google AI Studio](https://makersuite.google.com/app/apikey)

## Setup Instructions

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd FitnessCenter
   ```

2. **Configure the connection string**
   - Open `appsettings.json`
   - Update the `DefaultConnection` string if needed (default uses SQLite: `Data Source=FitnessCenter.db`)

3. **Configure Google Gemini API Key**
   - Open `appsettings.json`
   - Replace `your-gemini-api-key-here` in `GoogleGemini:ApiKey` with your actual Google Gemini API key
   - Get your API key from [Google AI Studio](https://makersuite.google.com/app/apikey)

4. **Restore packages and build**
   ```bash
   dotnet restore
   dotnet build
   ```

5. **Run database migrations** (if needed)
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
   
   Note: The application will automatically create the database and seed initial data on first run.

6. **Run the application**
   ```bash
   dotnet run
   ```

7. **Access the application**
   - Navigate to `https://localhost:5001` or `http://localhost:5000`
   - Login as admin: `ogrencinumarasi@sakarya.edu.tr` / `sau`

## Default Admin Credentials

- **Email**: `ogrencinumarasi@sakarya.edu.tr` / `g201210589@sakarya.edu.tr`
- **Password**: `sau`

## Project Structure

```
FitnessCenter/
├── Controllers/
│   ├── HomeController.cs
│   ├── AccountController.cs
│   ├── AdminController.cs
│   ├── TrainerController.cs
│   ├── MemberController.cs
│   ├── AppointmentController.cs
│   ├── ServiceController.cs
│   ├── AIController.cs
│   └── API/
│       └── ApiController.cs
├── Models/
│   ├── FitnessCenter.cs
│   ├── Trainer.cs
│   ├── Member.cs
│   ├── Service.cs
│   ├── Appointment.cs
│   ├── WorkingHours.cs
│   └── TrainerExpertise.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   └── DbInitializer.cs
├── Services/
│   ├── AppointmentService.cs
│   ├── AIService.cs
│   └── EmailService.cs
└── Views/
    ├── Shared/ (Layout, _LoginPartial)
    ├── Home/
    ├── Account/
    ├── Admin/
    ├── Trainer/
    ├── Member/
    ├── Appointment/
    ├── Service/
    └── AI/
```

## Database Models

- **FitnessCenter**: Center information and working hours
- **Trainer**: Trainer details, expertise areas, availability
- **Member**: Extends IdentityUser, personal info, body metrics
- **Service**: Service types (Fitness, Yoga, Pilates), duration, price
- **Appointment**: Member, Trainer, Service, DateTime, Status, Duration, Price
- **WorkingHours**: Day of week, Start/End time (for FitnessCenter and Trainer)
- **TrainerExpertise**: Many-to-many relationship between Trainer and Service

## API Endpoints

All API endpoints require authentication. Admin endpoints require Admin role.

### GET /api/api/trainers
Returns list of all trainers with their expertise.

### GET /api/api/trainers/available
Query parameters:
- `date` (required): DateTime in ISO format
- `serviceId` (required): Service ID

Returns available trainers for the specified date and service.

### GET /api/api/appointments/member/{memberId}
Returns all appointments for a specific member (Admin only).

### GET /api/api/appointments/trainer/{trainerId}
Returns all appointments for a specific trainer.

### GET /api/api/services
Returns list of all services.

## Security Considerations

- Role-based authorization on all admin actions
- Input validation (client-side and server-side)
- SQL injection prevention (EF Core parameterized queries)
- XSS protection
- Secure file upload for photos

## AI Features

### Exercise Recommendations
- Upload a photo and get personalized exercise recommendations
- Optionally provide body metrics (height, weight, gender, age) for more accurate recommendations
- Access via: `/AI` (Members only)

### Diet Plan Recommendations
- Get personalized diet and nutrition plans based on body metrics
- Specify fitness goals (weight loss, muscle building, etc.)
- Access via: `/AI/DietPlan` (Members only)

## Notes

- The application uses SQLite by default (`FitnessCenter.db` file). For production, consider using SQL Server or PostgreSQL.
- Google Gemini API key must be configured for AI features to work.
- Email service is currently a placeholder - implement actual email sending for production use.
- Photo uploads are stored in `wwwroot/uploads/ai-photos/` directory.
- Password requirements: Minimum 6 characters, at least one digit, one uppercase, and one lowercase letter.

## License

This project is created for educational purposes.

