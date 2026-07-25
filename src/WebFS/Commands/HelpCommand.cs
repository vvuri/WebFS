namespace WebFS;

public class HelpCommand : ICommand
{
    public string Name => "help";
    
    public void Execute(string[] args)
    {
        Console.WriteLine("WebFS commands:");
        Console.WriteLine("- help");
        Console.WriteLine("- clear");
        Console.WriteLine("- crawl <url>");
        Console.WriteLine("- crawl <url> <username> <password>");
        Console.WriteLine("- ls | ls -l");
        Console.WriteLine("- tree");
        Console.WriteLine("- cd <page> | cd .. | cd ~");
        Console.WriteLine("- cat <filename>");
        Console.WriteLine("- top <filename>");
        Console.WriteLine("- exit");
    }
}