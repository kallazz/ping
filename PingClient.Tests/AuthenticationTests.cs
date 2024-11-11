namespace PingClient.Tests;

using PingClient;
using NUnit.Framework;
using System.Reflection;
using System.Threading.Tasks;
using Moq;

public class AuthenticationTests
{
    private Mock<IDatabaseService> _mockDatabaseService;
    private Authentication _authentication;

    [SetUp]
    public void Setup()
    {
        _mockDatabaseService = new Mock<IDatabaseService>();
        _authentication = new Authentication(_mockDatabaseService.Object);
    }

    [Test]
    public async Task TestTooShortUsername()
    {
        var result = await _authentication.RegisterUser("abc", "name@mail.com", "MyPassword1!", "MyPassword1!");
        var expectedResult = ValidationError.UsernameTooShort;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestInvalidCharactersUsername()
    {
        var result = await _authentication.RegisterUser("abc!", "name@mail.com", "MyPassword1!", "MyPassword1!");
        var expectedResult = ValidationError.UsernameInvalidCharacters;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestPasswordsDoNotMatch()
    {
        var result = await _authentication.RegisterUser("validUser", "name@mail.com", "MyPassword1!", "OtherPassword");
        var expectedResult = ValidationError.PasswordsDoNotMatch;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestPasswordTooShort()
    {
        var result = await _authentication.RegisterUser("validUser", "name@mail.com", "short", "short");
        var expectedResult = ValidationError.PasswordTooShort;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    [TestCase("password123")]
    [TestCase("password123$")]
    [TestCase("Password123")]
    [TestCase("!Password!$#")]
    [TestCase("Password$!&$")]
    public async Task TestInvalidPasswordFormat(string password)
    {
        var result = await _authentication.RegisterUser("validUser", "name@mail.com", password, password);
        var expectedResult = ValidationError.PasswordInvalidFormat;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestInvalidEmailFormat()
    {
        var result = await _authentication.RegisterUser("validUser", "invalid-email", "MyPassword1!", "MyPassword1!");
        var expectedResult = ValidationError.EmailInvalidFormat;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestDatabaseError()
    {
        _mockDatabaseService.Setup(db => db.InsertUserIntoDatabase(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync(false);

        var result = await _authentication.RegisterUser("validUser", "name@mail.com", "ValidPassword1!", "ValidPassword1!");
        var expectedResult = ValidationError.DatabaseError;

        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public async Task TestValidRegistration()
    {
        _mockDatabaseService.Setup(db => db.InsertUserIntoDatabase(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .ReturnsAsync(true);

        var result = await _authentication.RegisterUser("ValidUser1", "valid@email.com", "ValidPassword1!", "ValidPassword1!");
        var expectedResult = ValidationError.None;

        Assert.That(result, Is.EqualTo(expectedResult));
    }
}
