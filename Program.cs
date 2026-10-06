namespace DecklistChecker;

public class Program
{
    public static string BaseDirectory = AppContext.BaseDirectory;
    // The build output lives in bin/<Configuration>/<TargetFramework>/, so the Decklists folder is three levels up.
    public static string DecklistsDirectory = Path.GetFullPath(Path.Combine(BaseDirectory, "..", "..", "..", "Decklists"));
    //string that will save the input within runtime
    public static string CommanderName = "";
    //Contains all the text files within the Directory
    public static string[] DecklistFiles = [];
    private static void Main(string[] args)
    {
        Console.WriteLine("Enter Commander Name: ");
        CommanderName = Console.ReadLine() ?? "";
        Console.WriteLine("Commander Searched: " + CommanderName);
        DecklistsDirectory = Path.Combine(DecklistsDirectory, CommanderName);
        Console.WriteLine(DecklistsDirectory);

        if (!Directory.Exists(DecklistsDirectory))
        {
            Console.WriteLine("No decklists found for " + CommanderName);
            return;
        }

        DecklistFiles = Directory.GetFiles(DecklistsDirectory, "*.txt", SearchOption.AllDirectories);

        foreach (string Decklist in DecklistFiles)
        {
            Console.WriteLine(Decklist);
        }
    }
}
