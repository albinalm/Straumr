namespace Straumr.Cli.Models;

public record ErrorDetail(string Message);

public record ErrorEnvelope(ErrorDetail Error);
