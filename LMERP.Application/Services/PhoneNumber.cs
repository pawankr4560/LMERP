using System.Text.RegularExpressions;
using LMERP.Application.Exceptions;

namespace LMERP.Application.Services;

public sealed class CustomerPhoneOptions
{
    public string DefaultCountryCode { get; set; } = "+91";
}

public static partial class PhoneNumber
{
    public static string Normalize(string input, CustomerPhoneOptions? options = null)
    {
        var number = Regex.Replace(input ?? string.Empty, @"[\s\-\.()]", string.Empty);
        if (number.StartsWith("00", StringComparison.Ordinal)) number = "+" + number[2..];
        var code = options?.DefaultCountryCode ?? "+91";
        if (Regex.IsMatch(number, @"^\d{10}$")) number = code + number;
        else if (Regex.IsMatch(number, @"^0\d{10}$")) number = code + number[1..];
        if (!E164Regex().IsMatch(number)) throw new DomainValidationException("Enter a valid phone number.");
        return number;
    }

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164Regex();
}
