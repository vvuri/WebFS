namespace WebFS;

public class HelpCommand : ICommand
{
    public string Name => "help";
    
    public void Execute(string[] args)
    {
        Console.WriteLine("Help Command");
    }
}