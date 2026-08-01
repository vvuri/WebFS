namespace WebFS;

public class CommandProcessor
{
    private readonly Dictionary<string, ICommand> _commands;

    public CommandProcessor(SessionContext context)
    {
        _commands = new Dictionary<string, ICommand>()
        {
            { "help", new HelpCommand() },
            { "clear", new ClearCommand() },
            { "mount", new MountCommand(context) },
            { "unmount", new UnmountCommand(context) },
            { "exit", new ExitCommand(context) }
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