using System.Reflection;

namespace Straumr.Console.Cli.Infrastructure;

internal static class AgentDocs
{
    public const string Flag = "--agent-help";
    public const string Description = "Prints the agent operating guide for AI agents and scripts";

    private const string ResourceName = "Straumr.Console.Cli.AgentDocs.md";

    public static int Write()
    {
        Assembly assembly = typeof(AgentDocs).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            System.Console.Error.WriteLine("Agent documentation was not embedded in this build.");
            return 1;
        }

        using StreamReader reader = new(stream);
        string text = reader.ReadToEnd();

        System.Console.Out.Write(text);
        if (!text.EndsWith('\n'))
        {
            System.Console.Out.WriteLine();
        }

        return 0;
    }
}
