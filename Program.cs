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

        //turns a commander name into a standard form so capitalization, word order and separators don't matter
        //e.g. "Tymna Kraum", "kraum/tymna" and "Kraum + Tymna" all become "kraum tymna"
        static string NormalizeName(string Name) =>
            string.Join(" ", Name.ToLower()
                                 .Split(new[] { ' ', '/', '+', '&', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Where(Word => Word != "and")
                                 .Order());

        //find the folder inside Decklists whose name matches the search once both are in the standard form
        //NOTE: folders should be named with the commanders' short names (e.g. "Kefka", "Tymna Kraum"), in any order
        string? CommanderFolder = Directory.GetDirectories(DecklistsDirectory)
            .FirstOrDefault(Folder => NormalizeName(Path.GetFileName(Folder)) == NormalizeName(CommanderName));

        //failcase handled if directory doesn't exist
        if (CommanderFolder == null)
        {
            Console.WriteLine("No decklists found for " + CommanderName);
            return;
        }
        DecklistsDirectory = CommanderFolder;

        //grabs all .txt files in the appropriate directory
        //SearchOption.AllDirectories also picks up .txt files in any subfolders, so decklists can be grouped further later on
        DecklistFiles = Directory.GetFiles(DecklistsDirectory, "*.txt", SearchOption.AllDirectories);

        //Loops to handle the behavior of all the .txt files in directory
        foreach (string Decklist in DecklistFiles)
        {
            Console.WriteLine(Decklist);
            //read the file once so the line count and the contents come from the same read
            //the decklist files are small (~100-140 lines), so loading the whole file into memory is fine
            //blank lines at the start/end are trimmed off and every line is trimmed so blank lines are exactly ""
            string[] Lines = File.ReadAllText(Decklist).Trim().Split('\n').Select(Line => Line.Trim()).ToArray();
            Console.WriteLine(Lines.Length);
            //the textfiles downloaded from topdeck.gg are split into blocks by blank lines:
            //first block = main deck, middle blocks = optional sections (SIDEBOARD:, STICKERS:, ATTRACTIONS:),
            //last block = the commander(s), 1 line for a single commander or 2 lines for partners (e.g. Tymna + Kraum)
            //so instead of counting to 99, we keep everything before the first blank line and after the last one
            int FirstBlank = Array.IndexOf(Lines, "");
            int LastBlank = Array.LastIndexOf(Lines, "");
            for (int Counter = 1; Counter <= Lines.Length; Counter++)
            {
                //catalog data for the main deck (before the first blank line) and the commander(s) (after the last one)
                //if a file has no blank lines at all, both indexes are -1 and every line is counted
                if ((Counter <= FirstBlank) || (Counter > LastBlank + 1))
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
        //uses the same list of files that was read above (which includes subfolders) so the percentages line up
        int txtFileCount = DecklistFiles.Length;

        foreach (KeyValuePair<string, int> DataPair in DataDict)
            Console.WriteLine($"CardName: {DataPair.Key, -30}, Shows Up: {(((float)(DataPair.Value)/txtFileCount)*100):F2}% Times");
    }
}
