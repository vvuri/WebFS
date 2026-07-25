namespace WebFS;

public class OpenCommand : ICommand
{
    public string Name => "open";

    public void Execute(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: open <url>");
        }
    }
}