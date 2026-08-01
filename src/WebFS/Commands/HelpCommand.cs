namespace WebFS;

public class HelpCommand : ICommand
{
    public string Name => "help";
    
    public void Execute(string[] args)
    {
        Console.WriteLine("WebFS commands:");
        Console.WriteLine("- help");
        Console.WriteLine("- clear");
        Console.WriteLine("- mount <url>");
        Console.WriteLine("- crawl");
        Console.WriteLine("- crawl <username> <password>");
        Console.WriteLine("- ls | ls -l");
        Console.WriteLine("- tree");
        Console.WriteLine("- cd <page> | cd .. | cd ~");
        Console.WriteLine("- cat <filename>");
        Console.WriteLine("- top <filename>");
        Console.WriteLine("- unmount");
        Console.WriteLine("- exit");
    }
}