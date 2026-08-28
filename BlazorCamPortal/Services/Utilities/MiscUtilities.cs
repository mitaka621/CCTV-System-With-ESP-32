using System.ComponentModel.DataAnnotations;

namespace CamPortal.Core.Utilities
{
    public static class MiscUtilities
    {
        public static string GetDisplayNameAttributeValue(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DisplayAttribute), false)
                .FirstOrDefault() as DisplayAttribute;
            return attribute?.Name ?? value.ToString();
        }

        public static bool ValidateModel<T>(T model)
        {
            return ValidateModel(model, out _);
        }

        public static bool ValidateModel<T>(T model, out ICollection<ValidationResult> validationResults)
        {
            validationResults = new List<ValidationResult>();

            if (model == null)
            {
                return false;
            }

            var context = new ValidationContext(model);

            return Validator.TryValidateObject(model, context, validationResults, validateAllProperties: true);
        }

        public static DateTime FloorToSecond(DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
        }

        public static string TimeElapsedUntilNowInWords(DateTime startTimeUTC)
        {
            var currentTime = DateTime.UtcNow;

            if (startTimeUTC > currentTime)
            {
                return "In the future";
            }

            var elapsedTime = currentTime - startTimeUTC;

            if (elapsedTime.TotalSeconds < 60)
            {
                var rounded = Math.Round(elapsedTime.TotalSeconds);
                return $"{rounded} second{(rounded == 1 ? "s" : string.Empty)} ago";
            }

            if (elapsedTime.TotalMinutes < 60)
            {
                var rounded = Math.Round(elapsedTime.TotalMinutes);
                return $"{rounded} minute{(rounded == 1 ? "s" : string.Empty)} ago";
            }

            if (elapsedTime.TotalHours < 24)
            {
                var rounded = Math.Round(elapsedTime.TotalHours);
                return $"{rounded} hour{(rounded == 1 ? "s" : string.Empty)} ago";
            }

            var finalRounded = Math.Round(elapsedTime.TotalDays);
            return $"{finalRounded} day{(finalRounded == 1 ? "s" : string.Empty)} ago";
        }
    }
}
