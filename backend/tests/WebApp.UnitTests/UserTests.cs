using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class UserTests
{
    private static Result<User> Register(
        string email = "Amira@Example.com", string password = "Secret123",
        string fullName = "  Amira Ben Ali ", string phone = "22 123 456") =>
        User.Register(email, password, fullName, phone, p => "hash:" + p);

    [Fact]
    public void Register_ValidInput_CreatesActiveBuyerWithNormalizedFields()
    {
        var user = Register().Value;

        Assert.Equal("amira@example.com", user.Email);
        Assert.Equal("Amira Ben Ali", user.FullName);
        Assert.Equal("+21622123456", user.Phone);
        Assert.Equal("hash:Secret123", user.PasswordHash);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal([Role.Buyer], user.Roles);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("")]
    public void Register_InvalidEmail_Fails(string email) =>
        Assert.Equal(DomainErrors.User.EmailInvalid, Register(email: email).Error);

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void Register_WeakPassword_Fails(string password) =>
        Assert.Equal(DomainErrors.User.PasswordTooWeak, Register(password: password).Error);

    [Fact]
    public void Register_BlankName_Fails() =>
        Assert.Equal(DomainErrors.User.FullNameRequired, Register(fullName: "   ").Error);

    [Theory]
    [InlineData("22123456", "+21622123456")]
    [InlineData("+216 22 123 456", "+21622123456")]
    [InlineData("00216-98.765.432", "+21698765432")]
    public void NormalizePhone_TunisianFormats_AreAccepted(string input, string expected) =>
        Assert.Equal(expected, User.NormalizePhone(input));

    [Theory]
    [InlineData("1234567")]
    [InlineData("+33612345678")]
    [InlineData("abc")]
    public void NormalizePhone_InvalidNumbers_ReturnNull(string input) =>
        Assert.Null(User.NormalizePhone(input));
}
