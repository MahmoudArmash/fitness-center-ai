using System.Text;
using FitnessCenter.DTOs;

namespace FitnessCenter.Services
{
    public static class PromptBuilder
    {
        public static string BuildPhotoAnalysisPrompt(BodyMetricsDto? metrics = null)
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Analyze this fitness photo and provide brief, concise personalized exercise recommendations. " +
                                    "Consider body type and posture. Be specific and actionable.");

            if (metrics?.HasAnyMetrics == true)
            {
                promptBuilder.AppendLine("\nAdditional user information:");
                if (metrics.Height.HasValue) promptBuilder.AppendLine($"- Height: {metrics.Height} cm");
                if (metrics.Weight.HasValue) promptBuilder.AppendLine($"- Weight: {metrics.Weight} kg");
                if (!string.IsNullOrEmpty(metrics.Gender)) promptBuilder.AppendLine($"- Gender: {metrics.Gender}");
                if (metrics.Age.HasValue) promptBuilder.AppendLine($"- Age: {metrics.Age} years");

                promptBuilder.AppendLine("\nUse this information along with the photo analysis to provide highly personalized recommendations. " +
                                       "Consider BMI, body composition, and age-appropriate exercises.");
            }

            promptBuilder.AppendLine("\nProvide brief recommendations (maximum 200 words): " +
                                   "3-5 key exercises with sets/reps, one workout routine suggestion, and one fitness goal. " +
                                   "Use bullet points and short paragraphs. Keep it concise.");

            return promptBuilder.ToString();
        }

        public static string BuildDietPlanPrompt(BodyMetricsDto metrics, string? fitnessGoal)
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Provide a personalized diet and nutrition plan based on the following information:");

            if (metrics.Height.HasValue) promptBuilder.AppendLine($"- Height: {metrics.Height} cm");
            if (metrics.Weight.HasValue) promptBuilder.AppendLine($"- Weight: {metrics.Weight} kg");
            if (!string.IsNullOrEmpty(metrics.Gender)) promptBuilder.AppendLine($"- Gender: {metrics.Gender}");
            if (metrics.Age.HasValue) promptBuilder.AppendLine($"- Age: {metrics.Age} years");
            if (!string.IsNullOrEmpty(fitnessGoal)) promptBuilder.AppendLine($"- Fitness Goal: {fitnessGoal}");

            promptBuilder.AppendLine("\nProvide a brief, concise diet plan (maximum 250 words) including:");
            promptBuilder.AppendLine("- Daily calorie recommendations");
            promptBuilder.AppendLine("- Macronutrient breakdown (proteins, carbs, fats)");
            promptBuilder.AppendLine("- 2-3 meal suggestions for breakfast, lunch, dinner");
            promptBuilder.AppendLine("- Key food recommendations");
            promptBuilder.AppendLine("- Hydration guidelines");

            promptBuilder.AppendLine("\nFormat: Use bullet points and short paragraphs. Keep it concise and actionable.");

            return promptBuilder.ToString();
        }

        public static string BuildExerciseVisualizationPrompt(
            string exerciseName, 
            BodyMetricsDto? metrics = null, 
            string? bodyDescription = null)
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine($"Create a detailed, vivid visualization description of a person performing the exercise '{exerciseName}'. ");
            promptBuilder.AppendLine("Provide a comprehensive description that includes:");

            if (!string.IsNullOrEmpty(bodyDescription))
            {
                promptBuilder.AppendLine($"\nThe person has the following characteristics: {bodyDescription}");
            }
            else if (metrics != null)
            {
                if (metrics.Height.HasValue && metrics.Weight.HasValue)
                {
                    var bmi = CalculateBMI(metrics.Height.Value, metrics.Weight.Value);
                    var bodyType = GetBodyTypeFromBMI(bmi);
                    promptBuilder.AppendLine($"\nThe person has a {bodyType} body type (height: {metrics.Height}cm, weight: {metrics.Weight}kg).");
                }

                if (!string.IsNullOrEmpty(metrics.Gender))
                {
                    promptBuilder.AppendLine($"The person is {metrics.Gender.ToLower()}.");
                }
            }

            promptBuilder.AppendLine("\nProvide a brief, vivid visualization description (maximum 150 words) that includes:");
            promptBuilder.AppendLine("- Body position and posture during the exercise");
            promptBuilder.AppendLine("- Key muscle groups being engaged");
            promptBuilder.AppendLine("- Proper form highlights");
            promptBuilder.AppendLine("- Overall appearance and setting");
            promptBuilder.AppendLine("\nFormat: Use short paragraphs. Make it vivid and inspiring but concise.");

            return promptBuilder.ToString();
        }

        public static string BuildBodyAnalysisPrompt()
        {
            return "Analyze this person's body type, physique, and physical characteristics. " +
                   "Provide a brief, professional description focusing on body build, muscle definition, and overall physique " +
                   "that would be useful for creating a fitness visualization. Keep it concise (2-3 sentences max).";
        }

        private static double CalculateBMI(decimal heightCm, decimal weightKg)
        {
            var heightM = (double)(heightCm / 100m);
            var heightMSquared = heightM * heightM;
            return (double)weightKg / heightMSquared;
        }

        private static string GetBodyTypeFromBMI(double bmi)
        {
            return bmi switch
            {
                < 18.5 => "slim",
                < 25 => "athletic and fit",
                < 30 => "muscular and strong",
                _ => "strong and powerful"
            };
        }
    }
}
