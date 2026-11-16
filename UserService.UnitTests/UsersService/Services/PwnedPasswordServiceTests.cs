using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UserService.UnitTests.GeneralMocks;
using UserService.UnitTests.UsersService.UsersMocks;
using UsersService.Application.UsersService.Services;

namespace UserService.UnitTests.UsersService.Services
{
    public class PwnedPasswordServiceTests
    {

        private static (string prefix, string suffix) GetHashParts(string password)
        {
            var sha1 = SHA1.HashData(Encoding.UTF8.GetBytes(password));
            var hex = Convert.ToHexString(sha1);
            return (hex[..5], hex[5..]);
        }

        private static PwnedPasswordService CreateService(
            FakeHttpMessageHandler handler,
            ILogger<PwnedPasswordService> logger)
        {
            return new PwnedPasswordService(new HttpClient(handler), logger);
        }

        private static FakeHttpMessageHandler OkHandler(string content) =>
            new((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            }));

        private static FakeHttpMessageHandler ErrorHandler(HttpStatusCode code) =>
            new((req, ct) => Task.FromResult(new HttpResponseMessage(code)));

        [Fact]
        public async Task IsPasswordPwned_PasswordFoundInBreach_ReturnsTrue()
        {
            // Arrange
            const string password = "password123";
            var (_, suffix) = GetHashParts(password);

            var fakeResponse =
                $"0000000000000000000000000000000000A:1\r\n" +
                $"{suffix}:3861493\r\n" +
                $"FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFB:5\r\n";

            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(OkHandler(fakeResponse), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned(password, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsPasswordPwned_PasswordNotInBreach_ReturnsFalse()
        {
            // Arrange
            const string password = "MySuperUniquePassword_2024!@#";

            var fakeResponse =
                $"0000000000000000000000000000000000A:1\r\n" +
                $"1111111111111111111111111111111111B:2\r\n";

            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(OkHandler(fakeResponse), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned(password, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPasswordPwned_ApiReturns500_ReturnsFalse()
        {
            // Arrange
            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(
                ErrorHandler(HttpStatusCode.InternalServerError), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned("anypassword", CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPasswordPwned_ApiUnavailable_ReturnsFalseAndLogsError()
        {
            // Arrange
            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(
                ErrorHandler(HttpStatusCode.ServiceUnavailable), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned("test", CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            spy.Has(LogLevel.Error, "unavailable").Should().BeTrue();
            spy.Count(LogLevel.Error).Should().Be(1);
        }

        [Fact]
        public async Task IsPasswordPwned_SendsCorrectUrlToApi()
        {
            // Arrange
            const string password = "test";
            var (prefix, _) = GetHashParts(password);
            var expectedUrl = $"https://api.pwnedpasswords.com/range/{prefix}";

            var handler = OkHandler("");
            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(handler, spy.Logger);

            // Act
            await service.IsPasswordPwned(password, CancellationToken.None);

            // Assert
            handler.LastRequest.Should().NotBeNull();
            handler.LastRequest!.RequestUri!.ToString().Should().Be(expectedUrl);
        }

        [Fact]
        public async Task IsPasswordPwned_CancelledToken_ThrowsOperationCanceled()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(handler, spy.Logger);

            // Act & Assert
            var act = () => service.IsPasswordPwned("password", cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }


        [Fact]
        public async Task IsPasswordPwned_EmptyApiResponse_ReturnsFalse()
        {
            // Arrange
            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(OkHandler(""), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned("anypassword", CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPasswordPwned_CaseInsensitiveMatch_ReturnsTrue()
        {
            // Arrange
            const string password = "password";
            var (_, suffix) = GetHashParts(password);

            var fakeResponse = $"{suffix.ToLowerInvariant()}:100\r\n";

            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(OkHandler(fakeResponse), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned(password, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsPasswordPwned_KnownPassword_ReturnsTrue()
        {
            // Arrange
            const string password = "password";
            var (prefix, suffix) = GetHashParts(password);

            prefix.Should().Be("5BAA6");
            suffix.Should().Be("1E4C9B93F3F0682250B6CF8331B7EE68FD8");

            var fakeResponse = $"{suffix}:10175823\r\n";

            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(OkHandler(fakeResponse), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned(password, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsPasswordPwned_SuccessfulRequest_DoesNotLogError()
        {
            // Arrange
            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(OkHandler(""), spy.Logger);

            // Act
            await service.IsPasswordPwned("test", CancellationToken.None);

            // Assert
            spy.HasNo(LogLevel.Error).Should().BeTrue();
        }

        [Fact]
        public async Task IsPasswordPwned_ApiReturns404_ReturnsFalse()
        {
            // Arrange
            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(
                ErrorHandler(HttpStatusCode.NotFound), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned("test", CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            spy.Has(LogLevel.Error).Should().BeTrue();
        }

        [Fact]
        public async Task IsPasswordPwned_ApiRateLimited_ReturnsFalse()
        {
            // Arrange
            var spy = new LoggerSpy<PwnedPasswordService>();
            var service = CreateService(
                ErrorHandler(HttpStatusCode.TooManyRequests), spy.Logger);

            // Act
            var result = await service.IsPasswordPwned("test", CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }
    }
}
