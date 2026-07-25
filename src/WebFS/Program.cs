using WebFS;

Console.WriteLine("WebFS started.");

var processor = new CommandProcessor();

while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();

    processor.Execute(line);
}