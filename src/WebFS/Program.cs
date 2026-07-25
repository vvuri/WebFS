using WebFS;

Console.WriteLine("WebFS started.");

var processor = new CommandProcessor();

while (true)
{
    Console.Write("webfs> ");
    var line = Console.ReadLine();

    processor.Execute(line);
}