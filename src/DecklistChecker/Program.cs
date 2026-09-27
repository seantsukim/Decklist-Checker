using DecklistChecker;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: decklist-checker <path-to-decklist.txt>");
    return 2;
}

Decklist deck;
try
{
    deck = Decklist.Parse(File.ReadAllText(args[0]));
}
catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

Console.WriteLine($"Main deck: {deck.MainDeckCount} cards, Sideboard: {deck.SideboardCount} cards");

var errors = deck.Validate();
if (errors.Count == 0)
{
    Console.WriteLine("Decklist is legal.");
    return 0;
}

foreach (var error in errors)
    Console.WriteLine($"  - {error}");
return 1;
