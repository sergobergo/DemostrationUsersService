using FluentAssertions;
using MassTransit.Internals.ImTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UserService.UnitTests.GeneralMocks;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Services;
using UsersService.Domain;
using UsersService.Domain.Entities;

namespace UserService.UnitTests.UsersService.Services
{
    public class JtiCheckServiceTests : IDisposable
    {
        private readonly UserServiceDbContext _context;
        private readonly IRedisService _redis;
        private readonly LoggerSpy<JtiCheckService> _spy;
        private readonly JtiCheckService _service;

        public JtiCheckServiceTests()
        {
            var options = new DbContextOptionsBuilder<UserServiceDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new UserServiceDbContext(options);
            _redis = Substitute.For<IRedisService>();
            _spy = new LoggerSpy<JtiCheckService>();
            _service = new JtiCheckService(_redis, _spy.Logger, _context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private async Task SeedUserAsync(Guid id, Guid? jti)
        {
            _context.Users.Add(new UserEntity { Id = id, Jti = jti });
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task JtiCheck_FoundInRedis_MatchesJti_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var jti = Guid.NewGuid();

            _redis.GetDataAsync<string>(userId.ToString())
                .Returns(jti.ToString());

            // Act
            var result = await _service.JtiCheck(userId.ToString(), jti.ToString());

            // Assert
            result.Should().BeTrue();
            _spy.Has(LogLevel.Information, "Redis").Should().BeTrue();
            _spy.Has(LogLevel.Information, "Correct").Should().BeTrue();
        }


        [Fact]
        public async Task JtiCheck_FoundInRedis_DoesNotMatchJti_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var storedJti = Guid.NewGuid();
            var requestJti = Guid.NewGuid(); 

            _redis.GetDataAsync<string>(userId.ToString())
                .Returns(storedJti.ToString());

            // Act
            var result = await _service.JtiCheck(userId.ToString(), requestJti.ToString());

            // Assert
            result.Should().BeFalse();
            _spy.Has(LogLevel.Warning, "mismatch").Should().BeTrue();
        }

        [Fact]
        public async Task JtiCheck_NotInRedis_FoundInDb_MatchesJti_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var jti = Guid.NewGuid();

            _redis.GetDataAsync<string>(userId.ToString())
                .Returns((string?)null);

            await SeedUserAsync(userId, jti);

            // Act
            var result = await _service.JtiCheck(userId.ToString(), jti.ToString());

            // Assert
            result.Should().BeTrue();
            _spy.Has(LogLevel.Information, "Db").Should().BeTrue();
            _spy.Has(LogLevel.Information, "Correct").Should().BeTrue();
        }

        [Fact]
        public async Task JtiCheck_NotInRedis_FoundInDb_DoesNotMatchJti_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var storedJti = Guid.NewGuid();
            var requestJti = Guid.NewGuid();

            _redis.GetDataAsync<string>(userId.ToString())
                .Returns((string?)null);

            await SeedUserAsync(userId, storedJti);

            // Act
            var result = await _service.JtiCheck(userId.ToString(), requestJti.ToString());

            // Assert
            result.Should().BeFalse();
            _spy.Has(LogLevel.Warning, "not found").Should().BeTrue();
        }

        [Fact]
        public async Task JtiCheck_NotInRedis_NotInDb_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var jti = Guid.NewGuid();

            _redis.GetDataAsync<string>(userId.ToString())
                .Returns((string?)null);

            // Act
            var result = await _service.JtiCheck(userId.ToString(), jti.ToString());

            // Assert
            result.Should().BeFalse();
            _spy.Has(LogLevel.Warning, "not found").Should().BeTrue();
        }

        [Fact]
        public async Task JtiCheck_NotInRedis_UserWithNullJti_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var jti = Guid.NewGuid();

            _redis.GetDataAsync<string>(userId.ToString())
                .Returns((string?)null);

            await SeedUserAsync(userId, jti: null);

            // Act
            var result = await _service.JtiCheck(userId.ToString(), jti.ToString());

            // Assert
            result.Should().BeFalse();
            _spy.Has(LogLevel.Warning, "not found").Should().BeTrue();
        }

        [Fact]
        public async Task JtiCheck_FoundInRedis_DoesNotQueryDb()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var jti = Guid.NewGuid();

            _redis.GetDataAsync<string>(userId.ToString())
                .Returns(jti.ToString());

            // Act
            var result = await _service.JtiCheck(userId.ToString(), jti.ToString());

            // Assert
            result.Should().BeTrue();
            _spy.Has(LogLevel.Information, "Db").Should().BeFalse("не должен ходить в БД");
        }

        [Fact]
        public async Task JtiCheck_MultipleUsersInDb_FindsCorrectOne()
        {
            // Arrange
            var otherUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            var targetJti = Guid.NewGuid();
            var otherJti = Guid.NewGuid();

            _redis.GetDataAsync<string>(targetUserId.ToString())
                .Returns((string?)null);

            await SeedUserAsync(otherUserId, otherJti);
            await SeedUserAsync(targetUserId, targetJti);
            await SeedUserAsync(Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await _service.JtiCheck(targetUserId.ToString(), targetJti.ToString());

            // Assert
            result.Should().BeTrue();
        }
    }
}
