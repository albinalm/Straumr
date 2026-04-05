namespace Straumr.Cli.Models;

public record SendError(string Message);

public record SendErrorEnvelope(SendError Error);
