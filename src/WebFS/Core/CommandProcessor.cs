namespace WebFS;

public class CommandProcessor
{
    private readonly Dictionary<string, ICommand> _commands;

    public CommandProcessor()
    {
        _commands = new Dictionary<string, ICommand>()
        {
            { "help", new HelpCommand() },
            { "clear", new ClearCommand() },
            { "open", new OpenCommand() },
            { "exit", new ExitCommand() }
        };
    }

    public void Execute(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return;
        
        var cmd = input.Split(' ');
        
        if (_commands.TryGetValue(cmd[0], out var command))
        {
            command.Execute(cmd.Skip(1).ToArray());
        }
        else
        {
            Console.WriteLine("Command not found");
        }
    }
    
}