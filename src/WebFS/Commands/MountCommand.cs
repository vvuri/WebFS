using System.Net;

namespace WebFS;

public class MountCommand : ICommand
{
    public string Name => "open";
    
    private readonly SessionContext _context;

    public MountCommand(SessionContext context)
    {
        _context = context;
    }
    
    public void Execute(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: mount <url>");
            return;
        }
        
        // TODO: verify url
        
        String url = args[0];
        if (url.StartsWith("http://") || url.StartsWith("https://"))
        {
            var httpService = new HttpService();
                
            var statusCode = httpService
                .IsAvailableAsync(url)
                .GetAwaiter()
                .GetResult();

            if (statusCode == HttpStatusCode.OK)
            {
                _context.CurrentUrl = url;
                _context.Uuid = Guid.NewGuid();
            }

            Console.WriteLine(statusCode);
        }
        else
        {
            Console.Error.WriteLine("<url> not valid");
        }
    }
}