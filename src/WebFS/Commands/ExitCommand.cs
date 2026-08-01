namespace WebFS;

public class ExitCommand : ICommand
{
    public string Name => "exit";
    private readonly SessionContext _context;

    public ExitCommand(SessionContext context)
    {
        _context = context;
    }
    
    public void Execute(string[]  args)
    {
        if (_context.Uuid != Guid.Empty)
        {
            var uuid = _context.Uuid.ToString();
            Console.WriteLine("For return to session:\n> WebFS id=" + uuid);
        }

        // TODO: Read UUID session
        // TODO: Save to disk data as session
        Environment.Exit(0);
    }
}