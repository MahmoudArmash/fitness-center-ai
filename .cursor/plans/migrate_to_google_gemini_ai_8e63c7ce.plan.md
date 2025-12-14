---
name: Migrate to Google Gemini AI
overview: Replace OpenAI integration with Google Gemini AI Studio (AI Studio) for fitness recommendations, update configuration, and enhance AI features to support body metrics input and diet plan recommendations.
todos:
  - id: "1"
    content: Update appsettings.json to replace OpenAI config with Google Gemini config
    status: completed
  - id: "2"
    content: Modify AIService.cs to use Google Gemini API instead of OpenAI API
    status: completed
    dependencies:
      - "1"
  - id: "3"
    content: Update AIService to handle Gemini API request/response format
    status: completed
    dependencies:
      - "2"
  - id: "4"
    content: Enhance AIService to accept and use body metrics (height, weight, age, gender) in AI prompts
    status: completed
    dependencies:
      - "3"
  - id: "5"
    content: Update AIController to retrieve member body metrics from database and pass to AI service
    status: completed
    dependencies:
      - "4"
  - id: "6"
    content: Enhance AI/Index.cshtml view to show optional body metrics input fields
    status: completed
    dependencies:
      - "5"
  - id: "7"
    content: Add diet plan recommendation method to AIService and controller action
    status: completed
    dependencies:
      - "3"
  - id: "8"
    content: Test Gemini API integration with photo upload and body metrics
    status: completed
    dependencies:
      - "6"
      - "7"
---

# Fitness Center Project - Google Gemini AI Integration Plan

## Current State Analysis

The project already has most requirements implemented:

- ✅ CRUD operations for FitnessCenter, Service, Trainer
- ✅ REST API with LINQ queries (`/api/api/trainers`, `/api/api/trainers/available`, etc.)
- ✅ Admin panel with role-based authorization
- ✅ User registration and authentication
- ✅ Data validation (server-side and client-side)
- ✅ AI integration (currently using OpenAI)
- ✅ Appointment system with conflict detection

## Required Changes

### 1. Replace OpenAI with Google Gemini AI Studio

**Files to modify:**

- [`FitnessCenter/Services/AIService.cs`](FitnessCenter/Services/AIService.cs) - Replace OpenAI API calls with Google Gemini API
- [`FitnessCenter/appsettings.json`](FitnessCenter/appsettings.json) - Update configuration from OpenAI to Gemini
- [`FitnessCenter/appsettings.Development.json`](FitnessCenter/appsettings.Development.json) - Add Gemini config if needed

**Implementation details:**

- Google Gemini API endpoint: `https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent`
- Use API key authentication via query parameter: `?key={API_KEY}`
- Convert image to base64 and send in request body
- Handle Gemini's response format (different from OpenAI's structure)
- Update error handling for Gemini-specific errors

### 2. Enhance AI Service Interface

**Current method:**

```csharp
Task<string> AnalyzePhotoAndGetRecommendationsAsync(Stream photoStream, string fileName);
```

**Enhancement options:**

- Add overload method that accepts body metrics (height, weight, age, gender)
- Add method for diet plan recommendations
- Integrate member's body metrics from database when available

**File to modify:**

- [`FitnessCenter/Services/AIService.cs`](FitnessCenter/Services/AIService.cs) - Add new methods

### 3. Update AI Controller

**Files to modify:**

- [`FitnessCenter/Controllers/AIController.cs`](FitnessCenter/Controllers/AIController.cs) - Update to use enhanced AI service
- [`FitnessCenter/Views/AI/Index.cshtml`](FitnessCenter/Views/AI/Index.cshtml) - Add optional body metrics input form

**Enhancements:**

- Allow users to optionally provide height, weight, age, gender for more personalized recommendations
- Retrieve user's saved body metrics from Member profile if available
- Add separate endpoint for diet plan recommendations

### 4. Configuration Updates

**Update `appsettings.json`:**

```json
{
  "GoogleGemini": {
    "ApiKey": "your-gemini-api-key-here",
    "Model": "gemini-1.5-flash"
  }
}
```

**Remove:**

- `OpenAI` configuration section

### 5. Update Project Dependencies

**Check if needed:**

- Google Gemini SDK NuGet package (optional - can use HttpClient directly)
- If using SDK: `Google.AI.GenerativeLanguage` or similar package

**File to check:**

- [`FitnessCenter/FitnessCenter.csproj`](FitnessCenter/FitnessCenter.csproj) - Add Gemini SDK if preferred over direct HTTP calls

## Implementation Steps

1. **Update Configuration**

   - Replace OpenAI config with Gemini config in `appsettings.json`
   - Update `appsettings.Development.json` if it exists

2. **Modify AIService**

   - Replace OpenAI API endpoint with Gemini endpoint
   - Update request/response handling for Gemini format
   - Add method to include body metrics in AI prompts
   - Update error messages and logging

3. **Enhance AI Controller**

   - Modify `AnalyzePhoto` to optionally use member's body metrics
   - Add new action method for diet plan recommendations
   - Update views to show body metrics input (optional fields)

4. **Update Views**

   - Enhance `Index.cshtml` to show optional body metrics input
   - Pre-fill body metrics from user profile if available
   - Add diet plan recommendation section

5. **Testing**

   - Test photo upload with Gemini API
   - Test with body metrics input
   - Verify error handling
   - Test diet plan recommendations

## Google Gemini API Integration Details

**API Endpoint:**

```
POST https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={API_KEY}
```

**Request Format:**

```json
{
  "contents": [{
    "parts": [
      {
        "text": "Analyze this fitness photo and provide personalized exercise recommendations..."
      },
      {
        "inline_data": {
          "mime_type": "image/jpeg",
          "data": "base64_encoded_image"
        }
      }
    ]
  }]
}
```

**Response Format:**

```json
{
  "candidates": [{
    "content": {
      "parts": [{
        "text": "recommendations text..."
      }]
    }
  }]
}
```

## Notes

- The project already meets most requirements
- Main task is replacing OpenAI with Gemini
- Body metrics integration is optional enhancement but recommended for better personalization
- Diet plan feature can be added as additional functionality
- All existing features (CRUD, API, validation, authorization) remain unchanged