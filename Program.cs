namespace DecklistChecker;

public class Program
{
    public static string BaseDirectory = AppContext.BaseDirectory;
    // The build output lives in bin/<Configuration>/<TargetFramework>/, so the Decklists folder is three levels up.
    //NOTE: this only works when running from the build output (dotnet run / Visual Studio debug).
    //if the app is ever published or run from another folder, this path will be wrong and the Decklists
    //folder should be copied to the output (CopyToOutputDirectory in the .csproj) or passed in as an argument instead
    public static string DecklistsDirectory = Path.GetFullPath(Path.Combine(BaseDirectory, "..", "..", "..", "Decklists"));
    //string that will save the input within runtime
    public static string CommanderName = "";
    //Contains all the text files within the Directory
    public static string[] DecklistFiles = [];
    //holds all the data and how often it appears in the txt files data
    //key = card name, value = number of decklists the card shows up in
    public static Dictionary<string, int> DataDict = new();
    private static void Main(string[] args)
    {
        Console.WriteLine("Enter Commander Name: ");
        //ReadLine returns null when there is no more input (e.g. Ctrl+Z / Ctrl+D), so fall back to an empty name
        CommanderName = Console.ReadLine() ?? "";
        Console.WriteLine("Commander Searched: " + CommanderName);
        //NOTE: the commander name has to match the folder name inside Decklists (e.g. "Kefka").
        //on Linux/macOS this match is case sensitive, so "kefka" would not find the "Kefka" folder
        DecklistsDirectory = Path.Combine(DecklistsDirectory, CommanderName);

        //failcase handled if directory doesn't exist
        if (!Directory.Exists(DecklistsDirectory))
        {
            Console.WriteLine("No decklists found for " + CommanderName);
            return;
        }

        //grabs all .txt files in the appropriate directory
        //SearchOption.AllDirectories also picks up .txt files in any subfolders, so decklists can be grouped further later on
        DecklistFiles = Directory.GetFiles(DecklistsDirectory, "*.txt", SearchOption.AllDirectories);

        //Loops to handle the behavior of all the .txt files in directory
        foreach (string Decklist in DecklistFiles)
        {
            Console.WriteLine(Decklist);
            //read the file once so the line count and the contents come from the same read
            //the decklist files are small (~100-140 lines), so loading the whole file into memory is fine
            string[] Lines = File.ReadAllLines(Decklist);
            Console.WriteLine(Lines.Length);
            //we want to get the contents for the first 99 cards in these text files, plus the very last line
            //the textfiles downloaded from topdeck.gg is in this assumed format
            //NOTE: expected layout is lines 1-99 = main deck, then a blank line, then optional sections
            //(SIDEBOARD:, STICKERS:, ATTRACTIONS:), then the commander on the very last line.
            //this assumes a single commander with 99 one-of cards; partner commanders or a deck with
            //multiples of a card (e.g. "10 Island") will take up a different number of lines and need a
            //different approach, such as reading until the first blank line instead of counting to 99
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
                    //NOTE: the quantity itself is thrown away, so "10 Island" counts as 1 for that decklist.
                    //if quantities matter later, parse Content[..SpaceIndex] with int.TryParse and add that instead of 1
                    int SpaceIndex = Content.IndexOf(' ');
                    if (SpaceIndex >= 0)
                        Content = Content[(SpaceIndex + 1)..];
                    //update entry if it is in the dictionary, or make a new entry starting at 1 if it isn't
                    //GetValueOrDefault returns 0 for a card that isn't in the dictionary yet
                    DataDict[Content] = DataDict.GetValueOrDefault(Content) + 1;
                }
            }
        }
        //sort the pairs by how often the card shows up (most common first), then by card name for ties
        //NOTE: Dictionary keeps this order in practice as long as nothing is removed afterwards, but .NET doesn't
        //guarantee it. if the order needs to be guaranteed (or entries get removed later), switch DataDict to
        //OrderedDictionary<string, int> or keep the sorted result as a List<KeyValuePair<string, int>>
        DataDict = DataDict.OrderByDescending(DataPair => DataPair.Value)
                           .ThenBy(DataPair => DataPair.Key)
                           .ToDictionary(DataPair => DataPair.Key, DataPair => DataPair.Value);
        
        //Gets the number of .txt files in the directory to use in relation to frequency percentages
        int txtFileCount = Directory.GetFiles(DecklistsDirectory, "*.txt", SearchOption.TopDirectoryOnly).Length;

        foreach (KeyValuePair<string, int> DataPair in DataDict)
            Console.WriteLine($"CardName: {DataPair.Key, -30}, Shows Up: {(((float)(DataPair.Value)/txtFileCount)*100):F2}% Times");
    }
}
