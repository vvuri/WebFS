namespace WebFS;

public class UnmountCommand: ICommand
{
    public string Name => "unmount";
    
    private readonly SessionContext _context;

    public UnmountCommand(SessionContext context)
    {
        _context = context;
    }


    public void Execute(string[] args)
    {
        throw new NotImplementedException("отмонтировать ресурс и очистить все связи");
    }
}