using Microsoft.Extensions.Logging;
using NSubstitute;

namespace UserService.UnitTests.GeneralMocks
{
    public class LoggerSpy<T>
    {
        public ILogger<T> Logger { get; }
        public List<LogRecord> Records { get; } = new();

        public LoggerSpy()
        {
            Logger = Substitute.For<ILogger<T>>();

            Logger
                .WhenForAnyArgs(x => x.Log(default, default, default!, default, default!))
                .Do(callInfo =>
                {
                    var level = (LogLevel)callInfo[0];
                    var state = callInfo[2]?.ToString() ?? "";
                    var exception = callInfo[3] as Exception;

                    Records.Add(new LogRecord(level, state, exception));
                });
        }

        public bool Has(LogLevel level, string? messageContains = null) =>
            Records.Any(r => r.Level == level &&
                (messageContains is null ||
                 r.Message.Contains(messageContains, StringComparison.OrdinalIgnoreCase)));

        public int Count(LogLevel level) =>
            Records.Count(r => r.Level == level);

        public bool HasNo(LogLevel level) => Count(level) == 0;
    }

    public record LogRecord(LogLevel Level, string Message, Exception? Exception);
}
