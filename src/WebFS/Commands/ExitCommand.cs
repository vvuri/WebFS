namespace WebFS;

public class ExitCommand : ICommand
{
    public string Name => "exit";

    public void Execute(string[]  args)
    {
        var uuid = Guid.NewGuid().ToString();
        Console.WriteLine("For return to session:\n> WebFS id=" + uuid);
        // TODO: Read UUID session
        // TODO: Save to disk data as session
        Environment.Exit(0);
    }
}