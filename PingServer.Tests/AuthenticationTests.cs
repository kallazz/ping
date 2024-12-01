namespace PingServer.Tests;

using PingServer;
using NUnit.Framework;
using System.Threading.Tasks;
using Moq;

public class AuthenticationTests
{
    private Mock<IDatabaseService> mockDatabaseService;
    private Authentication sut;

    private string HashPassword(string password)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }

    public AuthenticationTests()
    {
        mockDatabaseService = new Mock<IDatabaseService>();
        sut = new Authentication(mockDatabaseService.Object);
    }

    /****************************************************************************************************/
    /**************************************** REGISTRATION TESTS ****************************************/
    /****************************************************************************************************/
    [Test]
    public async Task TestRegistrationTooShortUsername()
    {
        var result = await sut.RegisterUser("abc", "name@mail.com", "MyPassword1!", "MyPassword1!");
        var expectedResult = ValidationError.UsernameTooShort;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestRegistrationInvalidCharactersUsername()
    {
        var result = await sut.RegisterUser("abc!", "name@mail.com", "MyPassword1!", "MyPassword1!");
        var expectedResult = ValidationError.UsernameInvalidCharacters;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestRegistrationPasswordsDoNotMatch()
    {
        var result = await sut.RegisterUser("validUser", "name@mail.com", "MyPassword1!", "OtherPassword");
        var expectedResult = ValidationError.PasswordsDoNotMatch;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestRegistrationPasswordTooShort()
    {
        var result = await sut.RegisterUser("validUser", "name@mail.com", "short", "short");
        var expectedResult = ValidationError.PasswordTooShort;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    [TestCase("password123")]
    [TestCase("password123$")]
    [TestCase("Password123")]
    [TestCase("!Password!$#")]
    [TestCase("Password$!&$")]
    public async Task TestRegistrationInvalidPasswordFormat(string password)
    {
        var result = await sut.RegisterUser("validUser", "name@mail.com", password, password);
        var expectedResult = ValidationError.PasswordInvalidFormat;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestRegistrationInvalidEmailFormat()
    {
        var result = await sut.RegisterUser("validUser", "invalid-email", "MyPassword1!", "MyPassword1!");
        var expectedResult = ValidationError.EmailInvalidFormat;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestRegistrationDatabaseError()
    {
        mockDatabaseService.Setup(db => db.InsertUserIntoDatabase(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync(false);

        var result = await sut.RegisterUser("validUser", "name@mail.com", "ValidPassword1!", "ValidPassword1!");
        var expectedResult = ValidationError.DatabaseError;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestRegistrationSuccessful()
    {
        mockDatabaseService.Setup(db => db.InsertUserIntoDatabase(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync(true);

        var result = await sut.RegisterUser("ValidUser1", "valid@email.com", "ValidPassword1!", "ValidPassword1!");
        var expectedResult = ValidationError.None;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    /****************************************************************************************************/
    /******************************************* LOGIN TESTS ********************************************/
    /****************************************************************************************************/
    [Test]
    [TestCase("valid@email.com", "")]
    [TestCase("", "ValidPassword1!")]
    [TestCase("", "")]
    public async Task TestLoginEmptyInput(string email, string password)
    {
        var result = await sut.LoginUser(email, password);
        var expectedResult = ValidationError.InvalidCredentials;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestLoginByUsername()
    {
        mockDatabaseService.Setup(db => db.GetPasswordForUserByUsername(It.IsAny<string>()))
                            .ReturnsAsync("someHash");

        await sut.LoginUser("ValidUsername", "somePassword");

        mockDatabaseService.Verify(db => db.GetPasswordForUserByUsername(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task TestLoginByEmail()
    {
        mockDatabaseService.Setup(db => db.GetPasswordForUserByEmail(It.IsAny<string>()))
                            .ReturnsAsync("someHash");

        await sut.LoginUser("valid@email.com", "somePassword");

        mockDatabaseService.Verify(db => db.GetPasswordForUserByEmail(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task TestLoginNoPasswordFromDatabase()
    {
        var userPassword = "ValidPassword1!";

        mockDatabaseService.Setup(db => db.GetPasswordForUserByEmail(It.IsAny<string>()))
                            .ReturnsAsync((string?)null);

        var result = await sut.LoginUser("valid@email.com", userPassword);
        var expectedResult = ValidationError.InvalidCredentials;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestLoginInvalidPassword()
    {
        var userPassword = "ValidPassword1!";
        var wrongUserPassword = "WrongPassword1!";
        var hashedUserPassword = HashPassword(userPassword);

        mockDatabaseService.Setup(db => db.GetPasswordForUserByEmail(It.IsAny<string>()))
                            .ReturnsAsync(hashedUserPassword);

        var result = await sut.LoginUser("valid@email.com", wrongUserPassword);
        var expectedResult = ValidationError.InvalidCredentials;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestLoginSuccessful()
    {
        var userPassword = "ValidPassword1!";
        var hashedUserPassword = HashPassword(userPassword);

        mockDatabaseService.Setup(db => db.GetPasswordForUserByEmail(It.IsAny<string>()))
                            .ReturnsAsync(hashedUserPassword);

        var result = await sut.LoginUser("valid@email.com", userPassword);
        var expectedResult = ValidationError.None;

        Assert.That(result, Is.EqualTo(expectedResult));
    }
}