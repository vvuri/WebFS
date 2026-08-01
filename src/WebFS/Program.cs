using WebFS;

Console.WriteLine("WebFS started.");

var context = new SessionContext();
var processor = new CommandProcessor(context);

while (true)
{
    Console.Write("webfs> ");
    var line = Console.ReadLine();

    processor.Execute(line);
}