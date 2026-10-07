using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AcxiomCRM.Web.Validation;

/// <summary>
/// Validates a 10-digit Indian mobile number (starts with 6-9).
/// Applied server-side AND emitted as client-side validation via unobtrusive adapters.
/// </summary>
public class IndianPhoneAttribute : ValidationAttribute
{
    private static readonly Regex Pattern = new(@"^[6-9]\d{9}$", RegexOptions.Compiled);

    public IndianPhoneAttribute()
    {
        ErrorMessage = "Enter a valid phone number.";
    }

    public override bool IsValid(object? value)
    {
        if (value is null) return true; // [Required] handles emptiness
        var digits = Regex.Replace(value.ToString() ?? string.Empty, @"[\s\-+]", string.Empty);
        if (digits.StartsWith("91") && digits.Length == 12) digits = digits[2..];
        return Pattern.IsMatch(digits);
    }
}
