using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using UserService.UnitTests.GeneralMocks;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Exceptions;
using UsersService.Application.UsersService.Models;
using UsersService.Application.UsersService.Models.Settings;
using UsersService.Application.UsersService.Services;
using UsersService.Domain;
using UsersService.Domain.Entities;

namespace UserService.UnitTests.UsersService.Services
{
    public class AuthServiceTests : IDisposable
    {
        private readonly UserServiceDbContext _context;
        private readonly IRedisService _redis;
        private readonly LoggerSpy<AuthService> _spy;
        private readonly AuthService _service;
        private readonly JwtSettings _jwtSettings;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<UserServiceDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new UserServiceDbContext(options);
            _redis = Substitute.For<IRedisService>();
            _spy = new LoggerSpy<AuthService>();
            _jwtSettings = new JwtSettings
            {
                Key = "SuperSecretKeyForTestingThatIsAtLeast64CharactersLongForHmacSha512!!",
                Issuer = "TestIssuer",
                Audience = "TestAudience"
            };

            _service = new AuthService(
                _context,
                _redis,
                Options.Create(_jwtSettings),
                _spy.Logger);
        }

        public void Dispose() => _context.Database.EnsureDeleted();

        // ───────────────────── Helpers ─────────────────────

        private async Task<UserEntity> SeedUserAsync(
            string login = "testuser",
            string email = "test@test.com",
            string password = "TestPass123!",
            string? refreshToken = null,
            DateTime? refreshTokenExpiry = null)
        {
            var logDto = new UserLogDto { Login = login, Password = password };
            var hashed = new PasswordHasher<UserLogDto>().HashPassword(logDto, password);

            var user = new UserEntity
            {
                Id = Guid.NewGuid(),
                Login = login,
                Email = email,
                Name = "Test Name",
                PasswordHash = hashed,
                Role = "User",
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = refreshTokenExpiry ?? DateTime.UtcNow.AddDays(7)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        private void SetupRedisForTokenCreation()
        {
            _redis.GetDataAsync<string>(Arg.Any<string>())
                .Returns(Task.FromResult<string?>(null));
            _redis.SetJtiAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);
            _redis.RemoveAsync(Arg.Any<string>())
                .Returns(Task.CompletedTask);
        }

        // ═══════════════════ AddUserAsync ═══════════════════

        [Fact]
        public async Task AddUserAsync_LoginAlreadyExists_ThrowsUserAlreadyExistException()
        {
            await SeedUserAsync(login: "existing", email: "a@test.com");

            var newUser = new UserRegDto
            {
                Login = "existing",
                Email = "new@test.com",
                Password = "Pass123!",
                Name = "New"
            };

            var act = () => _service.AddUserAsync(newUser);

            await act.Should().ThrowAsync<UserAlreadyExistException>()
                .WithMessage("*already exists*");
        }

        [Fact]
        public async Task AddUserAsync_EmailAlreadyExists_ThrowsUserAlreadyExistException()
        {
            await SeedUserAsync(login: "user1", email: "same@test.com");

            var newUser = new UserRegDto
            {
                Login = "user2",
                Email = "same@test.com",
                Password = "Pass123!",
                Name = "New"
            };

            var act = () => _service.AddUserAsync(newUser);

            await act.Should().ThrowAsync<UserAlreadyExistException>();
        }

        [Fact]
        public async Task AddUserAsync_SaveFails_ThrowsDbSaveChangesException()
        {
            // Arrange 
            var failOptions = new DbContextOptionsBuilder<UserServiceDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var failContext = new FailingDbContext(failOptions);
            var failService = new AuthService(
                failContext, _redis, Options.Create(_jwtSettings), _spy.Logger);

            var newUser = new UserRegDto
            {
                Login = "newuser",
                Email = "new@test.com",
                Password = "Pass123!",
                Name = "New"
            };

            // Act
            var act = () => failService.AddUserAsync(newUser);

            // Assert
            await act.Should().ThrowAsync<DbSaveChangesException>();
            _spy.Has(LogLevel.Error, "Cannot save user").Should().BeTrue();
        }

        [Fact]
        public async Task AddUserAsync_Success_ReturnsUserLogDto_AndSavesToDb()
        {
            var newUser = new UserRegDto
            {
                Login = "newuser",
                Email = "new@test.com",
                Password = "Pass123!",
                Name = "New User"
            };

            var result = await _service.AddUserAsync(newUser);

            result.Should().NotBeNull();
            result.Login.Should().Be("newuser");

            var saved = await _context.Users
                .FirstOrDefaultAsync(u => u.Login == "newuser");
            saved.Should().NotBeNull();

            _spy.Has(LogLevel.Information, "succesful added").Should().BeTrue();
        }

        // ═══════════════════ LoginAsync ═══════════════════

        [Fact]
        public async Task LoginAsync_UserNotFound_ThrowsUserNotFoundException()
        {
            var user = new UserLogDto { Login = "ghost", Password = "Pass123!" };

            var act = () => _service.LoginAsync(user);

            await act.Should().ThrowAsync<UserNotFoundException>()
                .WithMessage("*ghost*");
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ThrowsWrongPasswordException()
        {
            await SeedUserAsync(login: "testuser", password: "CorrectPass!");

            var user = new UserLogDto { Login = "testuser", Password = "WrongPass!" };

            var act = () => _service.LoginAsync(user);

            await act.Should().ThrowAsync<WrongPasswordException>();
        }

        [Fact]
        public async Task LoginAsync_Success_ReturnsTokenResponseWithValidJwt()
        {
            SetupRedisForTokenCreation();
            var seeded = await SeedUserAsync(login: "testuser", password: "CorrectPass!");

            var user = new UserLogDto { Login = "testuser", Password = "CorrectPass!" };

            var result = await _service.LoginAsync(user);

            // AccessToken
            result.AccessToken.Should().NotBeNullOrEmpty();
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(result.AccessToken);
            jwt.Claims.Should().Contain(c =>
                c.Type == ClaimTypes.NameIdentifier && c.Value == seeded.Id.ToString());
            jwt.Claims.Should().Contain(c =>
                c.Type == JwtRegisteredClaimNames.Jti);

            // RefreshToken 
            result.RefreshToken.Should().NotBeNullOrEmpty();

            await _redis.Received(1)
                .SetJtiAsync(seeded.Id.ToString(), Arg.Any<string>());

            _spy.Has(LogLevel.Information, "JWT succesful created").Should().BeTrue();
        }

        // ═══════════════════ RefreshTokenAsync ═══════════════════

        [Fact]
        public async Task RefreshTokenAsync_UserNotFound_ThrowsDbSaveChangesException()
        {
            var request = new RefreshTokenRequestDto
            {
                UserId = Guid.NewGuid(),
                RefreshToken = "any-token"
            };

            var act = () => _service.RefreshTokenAsync(request);

            await act.Should().ThrowAsync<ValidateRefreshTokenException>();
        }

        [Fact]
        public async Task RefreshTokenAsync_InvalidToken_ThrowsDbSaveChangesException()
        {
            
            var user = await SeedUserAsync(refreshToken: "valid-token");

            var request = new RefreshTokenRequestDto
            {
                UserId = user.Id,
                RefreshToken = "wrong-token"
            };

            var act = () => _service.RefreshTokenAsync(request);

            var ex = await act.Should().ThrowAsync<ValidateRefreshTokenException>();
        }

        [Fact]
        public async Task RefreshTokenAsync_ExpiredToken_ThrowsDbSaveChangesException()
        {
            var user = await SeedUserAsync(
                refreshToken: "valid-token",
                refreshTokenExpiry: DateTime.UtcNow.AddDays(-1)); 

            var request = new RefreshTokenRequestDto
            {
                UserId = user.Id,
                RefreshToken = "valid-token"
            };

            var act = () => _service.RefreshTokenAsync(request);

            var ex = await act.Should().ThrowAsync<ValidateRefreshTokenException>();
        }

        [Fact]
        public async Task RefreshTokenAsync_Success_ReturnsNewTokens()
        {
            SetupRedisForTokenCreation();
            var user = await SeedUserAsync(
                refreshToken: "valid-token",
                refreshTokenExpiry: DateTime.UtcNow.AddDays(7));

            var request = new RefreshTokenRequestDto
            {
                UserId = user.Id,
                RefreshToken = "valid-token"
            };

            var result = await _service.RefreshTokenAsync(request);

            result.AccessToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBe("valid-token"); 

            _spy.Has(LogLevel.Information, "JWT succesful created").Should().BeTrue();
        }

        // ───────────── Вспомогательный контекст ─────────────

        private class FailingDbContext : UserServiceDbContext
        {
            public FailingDbContext(DbContextOptions<UserServiceDbContext> options)
                : base(options) { }

            public override Task<int> SaveChangesAsync(CancellationToken ct = default)
                => throw new Exception("Simulated DB failure");
        }
    }
}
