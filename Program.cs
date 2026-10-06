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
    //holds all the data and how often it appears in the txt files data
    public static Dictionary<string, int> DataDict = new();
    private static void Main(string[] args)
    {
        Console.WriteLine("Enter Commander Name: ");
        CommanderName = Console.ReadLine() ?? "";
        Console.WriteLine("Commander Searched: " + CommanderName);
        DecklistsDirectory = Path.Combine(DecklistsDirectory, CommanderName);

        //failcase handled if directory doesn't exist
        if (!Directory.Exists(DecklistsDirectory))
        {
            Console.WriteLine("No decklists found for " + CommanderName);
            return;
        }

        //grabs all .txt files in the appropriate directory
        DecklistFiles = Directory.GetFiles(DecklistsDirectory, "*.txt", SearchOption.AllDirectories);

        //Loops to handle the behavior of all the .txt files in directory
        foreach (string Decklist in DecklistFiles)
        {
            Console.WriteLine(Decklist);
            //read the file once so the line count and the contents come from the same read
            string[] Lines = File.ReadAllLines(Decklist);
            Console.WriteLine(Lines.Length);
            //we want to get the contents for the first 99 cards in these text files, plus the very last line
            //the textfiles downloaded from topdeck.gg is in this assumed format
            for (int Counter = 1; Counter <= Lines.Length; Counter++)
            {
                //catalog data for the first 99 and the last card (commander)
                if ((Counter <= 99) || (Counter == Lines.Length))
                {
                    string Content = Lines[Counter - 1].Trim();
                    //skip blank lines so they don't end up in the dictionary
                    if (Content.Length == 0)
                        continue;
                    //drop the quantity in front (everything up to the first space) so only the card name is left
                    int SpaceIndex = Content.IndexOf(' ');
                    if (SpaceIndex >= 0)
                        Content = Content[(SpaceIndex + 1)..];
                    //update entry if it is in the dictionary, or make a new entry starting at 1 if it isn't
                    DataDict[Content] = DataDict.GetValueOrDefault(Content) + 1;
                }
            }
        }
        //sort the pairs by how often the card shows up (most common first), then by card name for ties
        DataDict = DataDict.OrderByDescending(DataPair => DataPair.Value)
                           .ThenBy(DataPair => DataPair.Key)
                           .ToDictionary(DataPair => DataPair.Key, DataPair => DataPair.Value);
        foreach (KeyValuePair<string, int> DataPair in DataDict)
            Console.WriteLine($"CardName: {DataPair.Key}, Shows Up: {DataPair.Value}");
    }
}
