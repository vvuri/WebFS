namespace WebFS;

public class SessionContext
{
    public String? CurrentUrl { get; set; }

    public Guid? Uuid { get; set; } = Guid.Empty;
}