# Database Entity Relationship Model (ERM) - Text Description
## Fitness Center Management System

### Entities and Attributes

#### 1. Member
- **Primary Key**: Id (string, from IdentityUser)
- **Attributes**:
  - UserName (string)
  - Email (string, unique)
  - FirstName (string, max 100)
  - LastName (string, max 100)
  - Address (string, max 500)
  - DateOfBirth (datetime, nullable)
  - Gender (string, max 10)
  - Height (decimal(5,2), nullable) - in centimeters
  - Weight (decimal(5,2), nullable) - in kilograms
  - PhotoPath (string, nullable)
  - CreatedDate (datetime)
- **Relationships**: 
  - One-to-Many with Appointment

#### 2. FitnessCenter
- **Primary Key**: Id (int)
- **Attributes**:
  - Name (string, max 200, required)
  - Address (string, max 500, required)
  - Phone (string, max 20, nullable)
  - Email (string, max 100, nullable)
  - CreatedDate (datetime)
- **Relationships**:
  - One-to-Many with Trainer
  - One-to-Many with Service
  - One-to-Many with WorkingHours

#### 3. Trainer
- **Primary Key**: Id (int)
- **Attributes**:
  - FirstName (string, max 100, required)
  - LastName (string, max 100, required)
  - Email (string, max 100, nullable)
  - Phone (string, max 20, nullable)
  - Bio (string, max 500, nullable)
  - FitnessCenterId (int, Foreign Key, required)
- **Relationships**:
  - Many-to-One with FitnessCenter
  - One-to-Many with Appointment
  - One-to-Many with WorkingHours
  - Many-to-Many with Service (via TrainerExpertise)

#### 4. Service
- **Primary Key**: Id (int)
- **Attributes**:
  - Name (string, max 100, required)
  - Type (ServiceType enum: Fitness, Yoga, Pilates, required)
  - Description (string, max 500, required)
  - Price (decimal(18,2), required)
  - DurationMinutes (int, required)
  - FitnessCenterId (int, Foreign Key, required)
- **Relationships**:
  - Many-to-One with FitnessCenter
  - One-to-Many with Appointment
  - Many-to-Many with Trainer (via TrainerExpertise)

#### 5. Appointment
- **Primary Key**: Id (int)
- **Attributes**:
  - MemberId (string, Foreign Key, required)
  - TrainerId (int, Foreign Key, required)
  - ServiceId (int, Foreign Key, required)
  - AppointmentDateTime (datetime, required)
  - DurationMinutes (int, required)
  - Price (decimal(18,2), required)
  - Status (AppointmentStatus enum: Pending, Confirmed, Completed, Cancelled, required)
  - Notes (string, max 500, nullable)
  - CreatedDate (datetime)
- **Relationships**:
  - Many-to-One with Member
  - Many-to-One with Trainer
  - Many-to-One with Service

#### 6. WorkingHours
- **Primary Key**: Id (int)
- **Attributes**:
  - DayOfWeek (DayOfWeek enum: Monday-Sunday, required)
  - StartTime (TimeSpan, required)
  - EndTime (TimeSpan, required)
  - FitnessCenterId (int, Foreign Key, nullable)
  - TrainerId (int, Foreign Key, nullable)
- **Relationships**:
  - Many-to-One with FitnessCenter (optional)
  - Many-to-One with Trainer (optional)
- **Note**: Each record belongs to either FitnessCenter OR Trainer, not both

#### 7. TrainerExpertise
- **Composite Primary Key**: (TrainerId, ServiceId)
- **Attributes**:
  - TrainerId (int, Foreign Key, part of PK)
  - ServiceId (int, Foreign Key, part of PK)
- **Relationships**:
  - Many-to-One with Trainer
  - Many-to-One with Service
- **Purpose**: Junction table for many-to-many relationship between Trainer and Service

### Relationship Diagram (Text Format)

```
FitnessCenter (1) ────────< (N) Trainer
FitnessCenter (1) ────────< (N) Service
FitnessCenter (1) ────────< (N) WorkingHours
Trainer (1) ────────< (N) Appointment
Trainer (1) ────────< (N) WorkingHours
Service (1) ────────< (N) Appointment
Member (1) ────────< (N) Appointment
Trainer (M) ────────< (N) Service [via TrainerExpertise]
```

### Cardinality Summary

| Relationship | Type | Description |
|-------------|------|-------------|
| FitnessCenter → Trainer | 1:N | One fitness center employs many trainers |
| FitnessCenter → Service | 1:N | One fitness center offers many services |
| FitnessCenter → WorkingHours | 1:N | One fitness center has many working hours |
| Trainer → Appointment | 1:N | One trainer can have many appointments |
| Trainer → WorkingHours | 1:N | One trainer has many working hours |
| Service → Appointment | 1:N | One service can be booked in many appointments |
| Member → Appointment | 1:N | One member can make many appointments |
| Trainer ↔ Service | M:N | Many trainers can provide many services (via TrainerExpertise) |

### Foreign Key Constraints

1. **Trainer.FitnessCenterId** → FitnessCenter.Id (CASCADE DELETE)
2. **Service.FitnessCenterId** → FitnessCenter.Id (CASCADE DELETE)
3. **Appointment.MemberId** → Member.Id (RESTRICT DELETE)
4. **Appointment.TrainerId** → Trainer.Id (RESTRICT DELETE)
5. **Appointment.ServiceId** → Service.Id (RESTRICT DELETE)
6. **WorkingHours.FitnessCenterId** → FitnessCenter.Id (CASCADE DELETE, NULLABLE)
7. **WorkingHours.TrainerId** → Trainer.Id (CASCADE DELETE, NULLABLE)
8. **TrainerExpertise.TrainerId** → Trainer.Id
9. **TrainerExpertise.ServiceId** → Service.Id

### Enumerations

**ServiceType**:
- Fitness
- Yoga
- Pilates

**AppointmentStatus**:
- Pending
- Confirmed
- Completed
- Cancelled

**DayOfWeek**:
- Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday

### Special Notes

1. **Member** entity extends ASP.NET Core Identity's `IdentityUser`, inheriting authentication properties (Id, UserName, Email, PasswordHash, etc.)

2. **WorkingHours** has a special constraint: each record must belong to either a FitnessCenter OR a Trainer, but not both (enforced by nullable foreign keys)

3. **TrainerExpertise** is a pure junction table with composite primary key, enabling the many-to-many relationship between Trainers and Services

4. **Delete Behaviors**:
   - CASCADE: When a FitnessCenter is deleted, related Trainers, Services, and WorkingHours are deleted
   - RESTRICT: Appointments cannot be deleted if related Member, Trainer, or Service is deleted (prevents data loss)

5. **Data Precision**:
   - Height/Weight: decimal(5,2) - allows values like 175.50 cm, 75.25 kg
   - Price: decimal(18,2) - allows large monetary values with 2 decimal places
