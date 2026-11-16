
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Features.Validation;
using UsersService.Application.UsersService.Models;
using UsersService.Domain;

namespace UserService.UnitTests.UsersService.Features.Validation.Customs
{
    public class CustomValidatorTests
    {
        private readonly UserServiceDbContext _context;
        private readonly IPwnedPasswordService _pwnedPasswordServiceMock;
        private readonly ILogger<UserRegValidator> _loggerMock;
        private readonly UserRegValidator _validator;

        public CustomValidatorTests()
        {
            var options = new DbContextOptionsBuilder<UserServiceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

            _context = new UserServiceDbContext(options);
            _pwnedPasswordServiceMock = Substitute.For<IPwnedPasswordService>();
            _loggerMock = Substitute.For<ILogger<UserRegValidator>>();

            _validator = new UserRegValidator(
                _context,
                _pwnedPasswordServiceMock,
                    _loggerMock);
            }
        //Name------------------------------------------------------------------------
        [Fact]
        public async Task ValidName_Should_ReturnErrorMessage_WhenNameEmpty()
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = "",
                Login = "Login",
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "Name is required");
        }
        [Fact]
        public async Task ValidName_Should_ReturnErrorMessage_WhenNameLengthShort()
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = "A",
                Login = "Login",
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "The minimum length of the name is 2 characters");
        }
        [Fact]
        public async Task ValidName_Should_ReturnErrorMessage_WhenNameLengthOutOfRange()
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = "Wofnmldxsmnfuclkjnvuidsnbgjkeuhnmdf",
                Login = "Login",
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "The maximum length of the name is 30 characters");
        }
        [Fact]
        public async Task ValidName_Should_ReturnErrorMessage_WhenNameContainWrongSymbols()
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = ".+-*/}{[]()+=_-boba",
                Login = "Login",
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "Name can contain only Latin and Russian letters, numbers, underscores (_) and hyphens (-).");
        }

        [Theory]
        [InlineData("-name")]
        [InlineData("_name")]
        [InlineData("name-")]
        [InlineData("name_")]
        [InlineData("_-name-_")]
        [InlineData("-name-")]
        public async Task ValidName_Should_ReturnErrorMessage_WhenNameUseUnderscoreOrHypen(string name)
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = name,
                Login = "Login",
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "Name cannot begin or end with an underscore (_) or a hyphen (-).");
        }
        //Login-------------------------------------------------------------------------------------------------
        [Fact]
        public async Task ValidLogin_Should_ReturnErrorMessage_WhenLoginEmpty()
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = "Name",
                Login = "",
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "Login is required");
        }
        [Theory]
        [InlineData("L")]
        [InlineData("Lo")]
        [InlineData("Log")]
        public async Task ValidLogin_Should_ReturnErrorMessage_WhenLoginLengthShort(string login)
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = "Name",
                Login = login,
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "The minimum length of the login is 4 characters");
        }
        [Fact]
        public async Task ValidLogin_Should_ReturnErrorMessage_WhenLoginLengthOutOfRange()
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = "Name",
                Login = "Wofnmldxsmnfuclkjnvuidsnbgjkeuhnmdf",
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "The maximum length of the login is 30 characters");
        }
        [Fact]
        public async Task ValidLogin_Should_ReturnErrorMessage_WhenNameContainWrongSymbols()
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = "Name",
                Login = ".+-*/}{[]()+=_-boba",
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "Login can contain only Latin letters, numbers, underscores (_) and hyphens (-).");
        }

        [Theory]
        [InlineData("-name")]
        [InlineData("_name")]
        [InlineData("name-")]
        [InlineData("name_")]
        [InlineData("_-name-_")]
        [InlineData("-name-")]
        public async Task ValidLogin_Should_ReturnErrorMessage_WhenLoginUseUnderscoreOrHypen(string login)
        {
            //Arrange
            var dto = new UserRegDto
            {
                Name = "Name",
                Login = login,
                Email = "Email@gmail.com",
                Password = "password"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "Login cannot begin or end with an underscore (_) or a hyphen (-).");
        }

    }
}
