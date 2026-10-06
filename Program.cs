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
    public static Dictionary<string, int> DataDict = new Dictionary<string, int>();
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
            int FileLineCount = File.ReadLines(Decklist).Count();
            Console.WriteLine(FileLineCount);
            //we want to get the contents for the first 99 cards in these text files, plus the very last line
            //the textfiles downloaded from topdeck.gg is in this assumed format
            using (StreamReader Reader = new StreamReader(Decklist))
            {
                string Content;
                int Counter = 1;
                while ((Content = Reader.ReadLine()) != null)
                {
                    //catalog data for the first 99 and the last card (commander)
                    if ((Counter <= 99) || (Counter == FileLineCount))
                    {
                        //index 2 and beyond as we ignore the number in front and the space
                        if (Content.Length >2)
                            Content = Content[2..];
                        //update entry if it is in the dicitonary
                        if (DataDict.ContainsKey(Content))
                        {
                            DataDict[Content] += 1;
                        }
                        //make new entry in dictionary if the key isn't found
                        else
                        {
                            DataDict[Content] = 1;
                        }
                    }
                    Counter++;
                }
            }
        }
        foreach (KeyValuePair<string, int> DataPair in DataDict)
            Console.WriteLine($"CardName: {DataPair.Key}, Shows Up: {DataPair.Value}");
    }
}
