using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UsersService.Domain;
using FluentAssertions;
using UsersService.Domain.Entities;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Features.Validation;
using UsersService.Application.UsersService.Models;

namespace UserService.UnitTests.UsersService.Features.Validation
{
    public class UserRegValidatorTests
    {
        private readonly UserServiceDbContext _context;
        private readonly IPwnedPasswordService _pwnedPasswordServiceMock;
        private readonly ILogger<UserRegValidator> _loggerMock;
        private readonly UserRegValidator _validator;

        public UserRegValidatorTests()
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

        [Fact]
        public async Task ValidLogin_Should_ReturnErrorMessage_WhenLoginNotUnique()
        {
            //Arrange
            await SeedUserAsync("Name", "Login123", "Email1");
            var dto = new UserRegDto
            {
                Name = "Name",
                Login = "Login123",
                Email = "Email@gmail.com",
                Password = "Password1"
            };
            _pwnedPasswordServiceMock
            .IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

            //Act
            var result = await _validator.ValidateAsync(dto);
            //Assert
            result.Errors.Should().Contain(
                c => c.ErrorMessage == "This login has already been registered");
        }
        
        private async Task SeedUserAsync(string name, string login, string email)
        {
            _context.Users.Add(new UserEntity
            {
                Name = name,
                Login = login,
                Email = email,
                PasswordHash = "Password",
                Role = "User"
            });
            await _context.SaveChangesAsync();
        }
    }
}
