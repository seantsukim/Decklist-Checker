using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace DecklistChecker;

//the result of reading every decklist in one commander folder
//DataDict = card name -> number of decklists the card shows up in (sorted most common first)
//FileCount = number of decklists read, used for the percentages
//Commanders = the commander(s) used by the most decklists in the folder, used for the homepage thumbnail
public record DecklistResult(Dictionary<string, int> DataDict, int FileCount, string[] Commanders);

public class Program
{
    public static string BaseDirectory = AppContext.BaseDirectory;
    // The build output lives in bin/<Configuration>/<TargetFramework>/, so the Decklists folder is three levels up.
    //NOTE: this only works when running from the build output (dotnet run / Visual Studio debug).
    //if the app is ever published or run from another folder, this path will be wrong and the Decklists
    //folder should be copied to the output (CopyToOutputDirectory in the .csproj) or passed in as an argument instead
    public static string DecklistsDirectory = Path.GetFullPath(Path.Combine(BaseDirectory, "..", "..", "..", "Decklists"));

    private static void Main(string[] args)
    {
        //starts the Blazor website; the pages live in App.razor (page shell) and Decklists.razor (homepage + commander page)
        //the pages are rendered on the server (static server rendering), so no JavaScript or interactivity setup is needed
        WebApplicationBuilder Builder = WebApplication.CreateBuilder(args);
        Builder.Services.AddRazorComponents();

        WebApplication Site = Builder.Build();
        Site.UseAntiforgery();
        Site.MapRazorComponents<App>();
        Site.Run();
    }

    //turns a commander name into a standard form so capitalization, word order and separators don't matter
    //e.g. "Tymna Kraum", "kraum/tymna" and "Kraum + Tymna" all become "kraum tymna"
    public static string NormalizeName(string Name) =>
        string.Join(" ", Name.ToLower()
                             .Split(new[] { ' ', '/', '+', '&', ',' }, StringSplitOptions.RemoveEmptyEntries)
                             .Where(Word => Word != "and")
                             .Order());

    //every folder inside Decklists is one commander (or partner pair), so this is the list of homepage thumbnails
    public static string[] GetCommanderFolders() =>
        Directory.GetDirectories(DecklistsDirectory).Select(Path.GetFileName).OfType<string>().Order().ToArray();

    //find the folder inside Decklists whose name matches the search once both are in the standard form
    //NOTE: folders should be named with the commanders' short names (e.g. "Kefka", "Tymna Kraum"), in any order
    public static string? FindCommanderFolder(string CommanderName) =>
        Directory.GetDirectories(DecklistsDirectory)
            .FirstOrDefault(Folder => NormalizeName(Path.GetFileName(Folder)) == NormalizeName(CommanderName));

    //reads every decklist in a commander folder and builds the dictionary of how often each card shows up
    //NOTE: everything is created fresh on each call (nothing is stored in static fields), because on a website
    //several people can load pages at the same time and their counts must not add onto each other
    public static DecklistResult ReadDecklists(string CommanderFolder)
    {
        //holds all the data and how often it appears in the txt files data
        //key = card name, value = number of decklists the card shows up in
        Dictionary<string, int> DataDict = new();
        //counts how many decklists use each commander (or partner pair) so the thumbnail can show the most common one
        Dictionary<string, int> CommanderCounts = new();

        //grabs all .txt files in the appropriate directory
        //SearchOption.AllDirectories also picks up .txt files in any subfolders, so decklists can be grouped further later on
        string[] DecklistFiles = Directory.GetFiles(CommanderFolder, "*.txt", SearchOption.AllDirectories);

        //Loops to handle the behavior of all the .txt files in directory
        foreach (string Decklist in DecklistFiles)
        {
            //read the file once so the line count and the contents come from the same read
            //the decklist files are small (~100-140 lines), so loading the whole file into memory is fine
            //blank lines at the start/end are trimmed off and every line is trimmed so blank lines are exactly ""
            string[] Lines = File.ReadAllText(Decklist).Trim().Split('\n').Select(Line => Line.Trim()).ToArray();
            //the textfiles downloaded from topdeck.gg are split into blocks by blank lines:
            //first block = main deck, middle blocks = optional sections (SIDEBOARD:, STICKERS:, ATTRACTIONS:),
            //last block = the commander(s), 1 line for a single commander or 2 lines for partners (e.g. Tymna + Kraum)
            //so instead of counting to 99, we keep everything before the first blank line and after the last one
            int FirstBlank = Array.IndexOf(Lines, "");
            int LastBlank = Array.LastIndexOf(Lines, "");
            List<string> FileCommanders = new();
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
                    //lines after the last blank line are this decklist's commander(s)
                    if (LastBlank >= 0 && Counter > LastBlank + 1)
                        FileCommanders.Add(Content);
                }
            }
            //"|" joins partner names into one key, since card names never contain it
            string CommanderKey = string.Join("|", FileCommanders);
            CommanderCounts[CommanderKey] = CommanderCounts.GetValueOrDefault(CommanderKey) + 1;
        }
        //sort the pairs by how often the card shows up (most common first), then by card name for ties
        //NOTE: Dictionary keeps this order in practice as long as nothing is removed afterwards, but .NET doesn't
        //guarantee it. if the order needs to be guaranteed (or entries get removed later), switch DataDict to
        //OrderedDictionary<string, int> or keep the sorted result as a List<KeyValuePair<string, int>>
        DataDict = DataDict.OrderByDescending(DataPair => DataPair.Value)
                           .ThenBy(DataPair => DataPair.Key)
                           .ToDictionary(DataPair => DataPair.Key, DataPair => DataPair.Value);

        //the commander(s) used by the most decklists in this folder
        //NOTE: some folders hold a few lists that use alternate-name printings of the commanders, so the most common wins,
        //and on a tie the one whose name matches the folder name wins (e.g. "Sisay, Weatherlight Captain" in the Sisay folder)
        string[] FolderWords = NormalizeName(Path.GetFileName(CommanderFolder)).Split(' ');
        string[] Commanders = CommanderCounts.OrderByDescending(Pair => Pair.Value)
                                             .ThenByDescending(Pair => FolderWords.Count(Word => Pair.Key.ToLower().Contains(Word)))
                                             .Select(Pair => Pair.Key.Split('|', StringSplitOptions.RemoveEmptyEntries))
                                             .FirstOrDefault() ?? [];

        //uses the same list of files that was read above (which includes subfolders) so the percentages line up
        return new DecklistResult(DataDict, DecklistFiles.Length, Commanders);
    }

    //card images come from Scryfall (https://scryfall.com/docs/api)
    //Scryfall asks every app to send a User-Agent and Accept header, so they're set once on a shared HttpClient
    private static readonly HttpClient Scryfall = CreateScryfallClient();
    //card name -> image link; card images never change, so each card is only looked up once while the site is running
    private static readonly ConcurrentDictionary<string, string> CardImageCache = new(StringComparer.OrdinalIgnoreCase);

    private static HttpClient CreateScryfallClient()
    {
        HttpClient Client = new() { BaseAddress = new Uri("https://api.scryfall.com/"), Timeout = TimeSpan.FromSeconds(15) };
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("DecklistChecker/1.0");
        Client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return Client;
    }

    //looks up the Scryfall image for each card name and returns card name -> image link
    //cards that can't be found (or if Scryfall can't be reached) are left out, and the page shows their name as text instead
    public static async Task<Dictionary<string, string>> GetCardImages(IEnumerable<string> CardNames)
    {
        List<string> Missing = CardNames.Distinct().Where(Name => !CardImageCache.ContainsKey(Name)).ToList();

        //Scryfall's /cards/collection endpoint takes up to 75 cards per request, so ~170 cards only needs 3 requests
        for (int Start = 0; Start < Missing.Count; Start += 75)
        {
            //Scryfall asks for a short pause between requests
            if (Start > 0)
                await Task.Delay(100);

            List<string> Batch = Missing.Skip(Start).Take(75).ToList();
            try
            {
                string RequestJson = JsonSerializer.Serialize(new { identifiers = Batch.Select(Name => new { name = Name }) });
                using HttpResponseMessage Response = await Scryfall.PostAsync("cards/collection",
                    new StringContent(RequestJson, Encoding.UTF8, "application/json"));
                if (!Response.IsSuccessStatusCode)
                    continue;

                using JsonDocument Json = JsonDocument.Parse(await Response.Content.ReadAsStringAsync());
                foreach (JsonElement Card in Json.RootElement.GetProperty("data").EnumerateArray())
                {
                    string? ImageUrl = GetImageUrl(Card);
                    if (ImageUrl == null)
                        continue;
                    //Scryfall returns double-faced and split cards as "Front // Back", but a decklist may only list the front,
                    //so the image is stored under the full name and under each face's name
                    CardImageCache[Card.GetProperty("name").GetString() ?? ""] = ImageUrl;
                    if (Card.TryGetProperty("card_faces", out JsonElement Faces))
                        foreach (JsonElement Face in Faces.EnumerateArray())
                            CardImageCache[Face.GetProperty("name").GetString() ?? ""] = ImageUrl;
                }
                //cards Scryfall answered for but had no image for are stored as "" so they aren't looked up again on every page load
                //(this only happens after a successful answer, so cards missed because Scryfall was unreachable are retried)
                foreach (string Name in Batch)
                    CardImageCache.TryAdd(Name, "");
            }
            catch (Exception Error) when (Error is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
            {
                //Scryfall couldn't be reached or sent something unexpected; skip this batch so the page still loads
            }
        }

        return CardNames.Distinct()
                        .Where(Name => CardImageCache.GetValueOrDefault(Name, "") != "")
                        .ToDictionary(Name => Name, Name => CardImageCache[Name]);
    }

    //normal cards have their image on the card itself, double-faced cards have one image per face (the front face is used)
    private static string? GetImageUrl(JsonElement Card)
    {
        if (Card.TryGetProperty("image_uris", out JsonElement Images))
            return Images.GetProperty("normal").GetString();
        if (Card.TryGetProperty("card_faces", out JsonElement Faces) &&
            Faces[0].TryGetProperty("image_uris", out JsonElement FaceImages))
            return FaceImages.GetProperty("normal").GetString();
        return null;
    }
}
