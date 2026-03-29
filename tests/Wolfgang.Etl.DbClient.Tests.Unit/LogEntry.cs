using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

[ExcludeFromCodeCoverage]
internal sealed class LogEntry
{
    public LogEntry(LogLevel level, string message, Exception? exception)
    {
        Level = level;
        Message = message;
        Exception = exception;
    }



    public LogLevel Level { get; }
    public string Message { get; }
    public Exception? Exception { get; }
}
