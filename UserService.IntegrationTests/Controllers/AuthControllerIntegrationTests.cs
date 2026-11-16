using FluentAssertions;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Models;
using UsersService.Application.UsersService.Models.Settings;
using UsersService.Domain;
using UsersService.Domain.Entities;

namespace UserService.IntegrationTests.Controllers
{
    public class AuthTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";
        public IRedisService RedisMock { get; private set; } = null!;
        public IMessageExchanger MessageExchangerMock { get; private set; } = null!;
        public ISmtpClient SmtpClientMock { get; private set; } = null!;
        private const string TestKey =
       "IntegrationTestSuperSecretKeyThatIsAtLeast64CharactersLongForHmacSha512!!";


        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<UserServiceDbContext>>();
                services.AddDbContext<UserServiceDbContext>(o =>
                    o.UseInMemoryDatabase(_dbName));

                RedisMock = Substitute.For<IRedisService>();
                services.RemoveAll<IRedisService>();
                services.AddSingleton(RedisMock);

                MessageExchangerMock = Substitute.For<IMessageExchanger>();
                services.RemoveAll<IMessageExchanger>();
                services.AddSingleton(MessageExchangerMock);

                SmtpClientMock = Substitute.For<ISmtpClient>();
                SmtpClientMock.IsConnected.Returns(true);
                SmtpClientMock.Capabilities.Returns(SmtpCapabilities.None);
                services.RemoveAll<ISmtpClient>();
                services.AddSingleton(SmtpClientMock);

                var jtiCheckMock = Substitute.For<IJtiCheckService>();
                jtiCheckMock.JtiCheck(Arg.Any<string>(), Arg.Any<string>())
                    .Returns(true);
                services.RemoveAll<IJtiCheckService>();
                services.AddScoped(_ => jtiCheckMock);

                services.RemoveAll<IConfigureOptions<JwtSettings>>();
                services.Configure<JwtSettings>(s =>
                {
                    s.Key = TestKey;
                    s.Issuer = "TestIssuer";
                    s.Audience = "TestAudience";
                });

                var pwnedMock = Substitute.For<IPwnedPasswordService>();
                pwnedMock.IsPasswordPwned(Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(false);
                services.RemoveAll<IPwnedPasswordService>();
                services.AddSingleton(pwnedMock);

                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey));
                        options.TokenValidationParameters.ValidIssuer = "TestIssuer";
                        options.TokenValidationParameters.ValidAudience = "TestAudience";
                    });
            });
        }

        public async Task<UserEntity> SeedUserAsync(
            string login = "testuser",
            string password = "TestPass123!",
            string role = "User",
            string email = "test@test.com")
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<UserServiceDbContext>();

            var logDto = new UserLogDto { Login = login, Password = password };
            var hash = new PasswordHasher<UserLogDto>().HashPassword(logDto, password);

            var user = new UserEntity
            {
                Id = Guid.NewGuid(),
                Login = login,
                Email = email,
                Name = "Test User",
                PasswordHash = hash,
                Role = role,
                RefreshToken = "seeded-refresh-token",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7),
                Jti = Guid.NewGuid()
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }
    }


    public class AuthControllerIntegrationTests : IClassFixture<AuthTestFactory>
    {
        private readonly AuthTestFactory _factory;
        private readonly HttpClient _client;

        public AuthControllerIntegrationTests(AuthTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }


        private async Task<string> LoginAndGetTokenAsync(
            string login = "testuser", string password = "TestPass123!")
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { Login = login, Password = password });

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var token = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
            return token!.AccessToken;
        }

        private HttpClient CreateAuthenticatedClient(string token)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [Fact]
        public async Task Verification_ValidRequest_Returns200WithLogin()
        {
            // Arrange
            _factory.RedisMock.SetAuthCodeAsync(Arg.Any<string>(), Arg.Any<UserRegDto>())
                .Returns(Task.CompletedTask);
            _factory.MessageExchangerMock
                .SendEmailMessageAsync(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var request = new
            {
                Login = "newuser",
                Email = "new@test.com",
                Password = "StrongPass1!",
                Name = "NewUser"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/verification", request);
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"STATUS: {response.StatusCode}");
            Console.WriteLine($"BODY: {body}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("newuser");
        }

        [Fact]
        public async Task Verification_InvalidRequest_Returns400()
        {
            var request = new
            {
                Login = "",
                Email = "not-an-email",
                Password = "123",
                Name = ""
            };

            var response = await _client.PostAsJsonAsync("/api/auth/verification", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }


        [Fact]
        public async Task Register_CorrectCode_Returns200WithToken()
        {
            // Arrange
            var storedUser = new UserRegDto
            {
                Login = "reguser",
                Email = "reg@test.com",
                Password = "StrongPass1!",
                Name = "Reg User",
                Code = "123456"
            };

            _factory.RedisMock.GetDataAsync<UserRegDto?>(Arg.Any<string>())
                .Returns(storedUser);
            _factory.RedisMock.RemoveAsync(Arg.Any<string>())
                .Returns(Task.CompletedTask);
            _factory.RedisMock.SetJtiAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);
            _factory.RedisMock.GetDataAsync<string>(Arg.Any<string>())
                .Returns(Task.FromResult<string?>(null));

            // Act
            var response = await _client.PostAsync(
                "/api/auth/register?login=reguser&code=123456", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var token = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
            token.Should().NotBeNull();
            token!.AccessToken.Should().NotBeNullOrEmpty();
            token.RefreshToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Register_WrongCode_Returns400()
        {
            // Arrange
            _factory.RedisMock.GetDataAsync<UserRegDto?>(Arg.Any<string>())
                .Returns(new UserRegDto { Code = "999999" });
            // Act
            var response = await _client.PostAsync(
                "/api/auth/register?login=reguser&code=000000", null);
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Wrong login or verification code");
        }

        [Fact]
        public async Task Login_ValidCredentials_Returns200WithToken()
        {
            // Arrange
            _factory.RedisMock.GetDataAsync<string>(Arg.Any<string>())
                .Returns(Task.FromResult<string?>(null));
            _factory.RedisMock.SetJtiAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);

            await _factory.SeedUserAsync(login: "loginuser", password: "Correct123!");

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { Login = "loginuser", Password = "Correct123!" });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var token = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
            token!.AccessToken.Should().NotBeNullOrEmpty();

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token.AccessToken);
            jwt.Claims.Should().Contain(c =>
                c.Type == ClaimTypes.Name && c.Value == "Test User");
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsError()
        {
            // Arrange
            await _factory.SeedUserAsync(login: "loginuser2", password: "Correct123!");

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { Login = "loginuser2", Password = "WrongPassword!" });
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_UserNotFound_Returns404()
        {
            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { Login = "ghost", Password = "any" });
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task RefreshToken_ValidToken_Returns200WithNewTokens()
        {
            // Arrange
            _factory.RedisMock.GetDataAsync<string>(Arg.Any<string>())
                .Returns(Task.FromResult<string?>(null));
            _factory.RedisMock.SetJtiAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);

            var user = await _factory.SeedUserAsync(
                login: "refreshuser",
                password: "Pass123!");

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/refresh-token",
                new { UserId = user.Id, RefreshToken = "seeded-refresh-token" });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var token = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
            token!.AccessToken.Should().NotBeNullOrEmpty();
            token.RefreshToken.Should().NotBe("seeded-refresh-token"); // новый
        }

        [Fact]
        public async Task MainPage_WithValidToken_Returns200()
        {
            // Arrange
            await _factory.SeedUserAsync(login: "authuser", password: "Pass123!");
            var token = await LoginAndGetTokenAsync("authuser", "Pass123!");

            // Act
            var authClient = CreateAuthenticatedClient(token);
            var response = await authClient.GetAsync("/api/auth/MainPage");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Hi, user!");
        }

        [Fact]
        public async Task MainPage_WithoutToken_Returns401()
        {
            // Act
            var response = await _client.GetAsync("/api/auth/MainPage");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Admin_WithAdminRole_Returns200()
        {
            // Arrange
            await _factory.SeedUserAsync(
                login: "adminuser", password: "Pass123!", role: "Admin");
            var token = await LoginAndGetTokenAsync("adminuser", "Pass123!");

            // Act
            var authClient = CreateAuthenticatedClient(token);
            var response = await authClient.GetAsync("/api/auth/admin");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Hi, admin!");
        }

        [Fact]
        public async Task Admin_WithUserRole_Returns403()
        {
            // Arrange
            await _factory.SeedUserAsync(
                login: "regularuser", password: "Pass123!", role: "User");
            var token = await LoginAndGetTokenAsync("regularuser", "Pass123!");

            // Act
            var authClient = CreateAuthenticatedClient(token);
            var response = await authClient.GetAsync("/api/auth/admin");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
