using AcxiomCRM.Web.Validation;

namespace AcxiomCRM.Tests;

/// <summary>IndianPhoneAttribute: 10-digit Indian mobile numbers starting with 6-9.</summary>
public sealed class IndianPhoneAttributeTests
{
    private readonly IndianPhoneAttribute _attr = new();

    [Theory]
    [InlineData("9812345678")]      // plain 10-digit
    [InlineData("6123456789")]      // starts with 6
    [InlineData("7123456789")]      // starts with 7
    [InlineData("8123456789")]      // starts with 8
    [InlineData("9123456789")]      // starts with 9
    [InlineData("+91 9812345678")] // country code + spaces
    [InlineData("91-9812345678")]   // country code + dashes
    [InlineData("98123 45678")]     // internal spaces
    public void IsValid_AcceptsValidNumbers(string phone)
        => Assert.True(_attr.IsValid(phone));

    [Theory]
    [InlineData("12345")]           // too short
    [InlineData("abcdefghij")]      // not digits
    [InlineData("2812345678")]      // starts with 2, not 6-9
    [InlineData("5812345678")]      // starts with 5, not 6-9
    [InlineData("09812345678")]     // 11 digits
    [InlineData("981234567")]       // 9 digits
    [InlineData("")]                // empty
    public void IsValid_RejectsInvalidNumbers(string phone)
        => Assert.False(_attr.IsValid(phone));

    [Fact]
    public void IsValid_Null_ReturnsTrue_RequiredHandlesEmptiness()
        => Assert.True(_attr.IsValid(null));
}
