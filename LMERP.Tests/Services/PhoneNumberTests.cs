using LMERP.Application.Exceptions;
using LMERP.Application.Services;
using Xunit;

namespace LMERP.Tests.Services;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("9876543210", "+919876543210")]
    [InlineData("09876543210", "+919876543210")]
    [InlineData("+91 (98765) 43210", "+919876543210")]
    [InlineData("0091-98765.43210", "+919876543210")]
    public void Normalize_ConvertsSupportedFormats(string input, string expected) => Assert.Equal(expected, PhoneNumber.Normalize(input));

    [Theory]
    [InlineData("12345")]
    [InlineData("+01234567890")]
    [InlineData("+91abc6543210")]
    public void Normalize_RejectsInvalidNumbers(string input) => Assert.Throws<DomainValidationException>(() => PhoneNumber.Normalize(input));

    [Fact]
    public void Normalize_MapsDifferentFormatsToSameDuplicateKey() =>
        Assert.Equal(PhoneNumber.Normalize("9876543210"), PhoneNumber.Normalize("+91 98765-43210"));
}
