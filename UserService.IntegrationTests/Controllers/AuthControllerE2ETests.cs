using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UserService.IntegrationTests.Infrastructure;
using UserService.IntegrationTests.Infrastructure.MailPit;
using UsersService.Application.UsersService.Models;

namespace UserService.IntegrationTests.Controllers
{
    public class AuthE2ETests : IClassFixture<IntegrationTestFactory>
    {
        private readonly IntegrationTestFactory _factory;
        private readonly HttpClient _client;
        private readonly MailPitClient _mailPit;

        public AuthE2ETests(IntegrationTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _mailPit = new MailPitClient(factory.MailPitApiUrl);
        }

        private async Task<string> WaitForCodeAsync(string email)
        {
            var mail = await _mailPit.GetLastEmailAsync(email, timeoutSeconds: 30);
            mail.Should().NotBeNull($"Email to {email} should arrive in MailPit");

            var text = (mail!.Text ?? "") + (mail.Text ?? "");
            var match = Regex.Match(text, @"([A-Z0-9]{6})");
            match.Success.Should().BeTrue($"Email body should contain 6-digit code. Body: {text}");

            return match.Groups[1].Value;
        }

        private HttpClient CreateAuthenticatedClient(string accessToken)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        private static string UniqueLogin() => $"u{Guid.NewGuid():N}"[..12];
        private static string UniqueEmail(string login) => $"{login}@test.com";
        private static string UniquePassword() => $"Xk9#m{Guid.NewGuid():N}"[..12] + "!";

        [Fact]
        public async Task FullFlow_Verification_RefreshCode_Register_Login_Refresh_MainPage()
        {
            await _mailPit.ClearAsync();

            var login = UniqueLogin();
            var email = UniqueEmail(login);
            var password = UniquePassword();
            var name = "TestUser";

            var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verification", new
            {
                Login = login,
                Email = email,
                Password = password,
                Name = name
            });

            verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                "Verification request should succeed");

            var firstCode = await WaitForCodeAsync(email);

            var refreshCodeResponse = await _client.PostAsync(
                $"/api/auth/refresh-code?login={login}", null);

            refreshCodeResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                "Refresh code should succeed");

            var secondCode = await WaitForCodeAsync(email);
            secondCode.Should().NotBe(firstCode, "New code should differ from old");

            var registerResponse = await _client.PostAsync(
                $"/api/auth/register?login={login}&code={secondCode}", null);

            registerResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                "Registration with correct code should succeed");

            var registerTokens = await registerResponse.Content
                .ReadFromJsonAsync<TokenResponseDto>();
            registerTokens.Should().NotBeNull();
            registerTokens!.AccessToken.Should().NotBeNullOrEmpty();
            registerTokens.RefreshToken.Should().NotBeNullOrEmpty();

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(registerTokens.AccessToken);
            jwt.Claims.Should().Contain(c =>
                c.Type == ClaimTypes.Name && c.Value == name);

            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                Login = login,
                Password = password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginTokens = await loginResponse.Content
                .ReadFromJsonAsync<TokenResponseDto>();
            loginTokens!.AccessToken.Should().NotBeNullOrEmpty();

            var loginJwt = handler.ReadJwtToken(loginTokens.AccessToken);
            var userId = loginJwt.Claims
                .First(c => c.Type == ClaimTypes.NameIdentifier).Value;

            var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh-token", new
            {
                UserId = userId,
                RefreshToken = loginTokens.RefreshToken
            });

            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var refreshedTokens = await refreshResponse.Content
                .ReadFromJsonAsync<TokenResponseDto>();
            refreshedTokens!.AccessToken.Should().NotBe(loginTokens.AccessToken);
            refreshedTokens.RefreshToken.Should().NotBe(loginTokens.RefreshToken);

            var authClient = CreateAuthenticatedClient(refreshedTokens.AccessToken);
            var mainPageResponse = await authClient.GetAsync("/api/auth/MainPage");

            mainPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await mainPageResponse.Content.ReadAsStringAsync();
            body.Should().Contain("Hi, user!");
        }

        [Fact]
        public async Task MainPage_WithoutToken_Returns401()
        {
            var response = await _client.GetAsync("/api/auth/MainPage");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AdminEndpoint_RegularUser_Returns403()
        {
            await _mailPit.ClearAsync();

            var login = UniqueLogin();
            var email = UniqueEmail(login);
            var password = UniquePassword();

            await _client.PostAsJsonAsync("/api/auth/verification", new
            {
                Login = login,
                Email = email,
                Password = password,
                Name = "RegUser"
            });

            var code = await WaitForCodeAsync(email);
            await _client.PostAsync(
                $"/api/auth/register?login={login}&code={code}", null);

            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                Login = login,
                Password = password
            });
            var tokens = await loginResponse.Content
                .ReadFromJsonAsync<TokenResponseDto>();

            var authClient = CreateAuthenticatedClient(tokens!.AccessToken);
            var response = await authClient.GetAsync("/api/auth/admin");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Login_WrongPassword_Returns400()
        {
            await _mailPit.ClearAsync();

            var login = UniqueLogin();
            var email = UniqueEmail(login);
            var password = UniquePassword();

            await _client.PostAsJsonAsync("/api/auth/verification", new
            {
                Login = login,
                Email = email,
                Password = password,
                Name = "RegUser"
            });
            var code = await WaitForCodeAsync(email);
            await _client.PostAsync(
                $"/api/auth/register?login={login}&code={code}", null);

            var response = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                Login = login,
                Password = "CompletelyWrongPassword123!"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_DuplicateLogin_ReturnsError()
        {
            await _mailPit.ClearAsync();

            var login = UniqueLogin();
            var email = UniqueEmail(login);
            var password = UniquePassword();

            await _client.PostAsJsonAsync("/api/auth/verification", new
            {
                Login = login,
                Email = email,
                Password = password,
                Name = "First"
            });
            var code1 = await WaitForCodeAsync(email);
            var reg1 = await _client.PostAsync(
                $"/api/auth/register?login={login}&code={code1}", null);
            reg1.StatusCode.Should().Be(HttpStatusCode.OK);
            await _mailPit.ClearAsync();
            var email2 = $"dup_{email}";
            var verify2 = await _client.PostAsJsonAsync("/api/auth/verification", new
            {
                Login = login,
                Email = email2,
                Password = password,
                Name = "Second"
            });

            verify2.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "Duplicate login should be rejected at verification stage");
        }

        [Fact]
        public async Task Register_WrongCode_Returns400()
        {
            await _mailPit.ClearAsync();

            var login = UniqueLogin();
            var email = UniqueEmail(login);
            var password = UniquePassword();

            await _client.PostAsJsonAsync("/api/auth/verification", new
            {
                Login = login,
                Email = email,
                Password = password,
                Name = "RegUser"
            });

            var response = await _client.PostAsync(
                $"/api/auth/register?login={login}&code=000000", null);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Wrong login or verification code");
        }
    }
}
