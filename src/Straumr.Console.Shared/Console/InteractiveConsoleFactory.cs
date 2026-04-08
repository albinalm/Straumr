namespace Straumr.Console.Shared.Console;

public static class InteractiveConsoleFactory
{
    private static Func<IInteractiveConsole>? _factory;

    public static void SetFactory(Func<IInteractiveConsole> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public static bool TrySetFactory(Func<IInteractiveConsole> factory)
    {
        if (_factory is not null)
        {
            return false;
        }

        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        return true;
    }

    public static IInteractiveConsole Create()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Interactive console factory has not been configured.");
        }

        return _factory();
    }
}
