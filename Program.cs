using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace DecklistChecker;

//the result of reading every decklist in one commander folder
//DataDict = card name -> number of decklists the card shows up in (sorted most common first)
//FileCount = number of decklists read, used for the percentages
//Commanders = the commander(s) used by the most decklists in the folder, used for the homepage thumbnail
//CardImages = card name -> Scryfall image link (cards without an image are left out)
public record DecklistResult(Dictionary<string, int> DataDict, int FileCount, string[] Commanders, Dictionary<string, string> CardImages);

//what Scryfall knows about one card
//Name = Scryfall's main name for the card. every printing with the same Scryfall card id (oracle_id) has the same main name,
//so alternate-name printings (e.g. "Shantotto's Coercion") come back as the original card (e.g. "Diabolic Intent")
public record CardInfo(string Name, string? ImageUrl);

public class Program
{
    public static readonly string BaseDirectory = AppContext.BaseDirectory;
    // The build output lives in bin/<Configuration>/<TargetFramework>/, so the Decklists folder is three levels up.
    //NOTE: this only works when running from the build output (dotnet run / Visual Studio debug).
    //if the app is ever published or run from another folder, this path will be wrong and the Decklists
    //folder should be copied to the output (CopyToOutputDirectory in the .csproj) or passed in as an argument instead
    public static readonly string DecklistsDirectory = Path.GetFullPath(Path.Combine(BaseDirectory, "..", "..", "..", "Decklists"));

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
    //ToLowerInvariant gives the same result on every computer (plain ToLower depends on the computer's language settings)
    public static string NormalizeName(string Name) =>
        string.Join(" ", Name.ToLowerInvariant()
                             .Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries)
                             .Where(Word => Word != "and")
                             .Order());

    //characters that can separate commander names in a search or folder name
    private static readonly char[] NameSeparators = [' ', '/', '+', '&', ','];

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
    public static async Task<DecklistResult> ReadDecklists(string CommanderFolder)
    {
        //holds all the data and how often it appears in the txt files data
        //key = card name, value = number of decklists the card shows up in
        Dictionary<string, int> DataDict = new();
        //counts how many decklists use each commander (or partner pair) so the thumbnail can show the most common one
        Dictionary<string, int> CommanderCounts = new();
        //the card names read from each decklist, and which of them are the commander(s)
        //cards are only counted after every name has been looked up on Scryfall, so alternate names can be merged first
        List<List<string>> FileCards = new();
        List<List<string>> FileCommanders = new();

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
            List<string> Cards = new();
            List<string> Commanders = new();
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
                    Cards.Add(Content);
                    //lines after the last blank line are this decklist's commander(s)
                    if (LastBlank >= 0 && Counter > LastBlank + 1)
                        Commanders.Add(Content);
                }
            }
            FileCards.Add(Cards);
            FileCommanders.Add(Commanders);
        }

        //look up every card on Scryfall so cards that share the same card id count as one card
        //(e.g. "Shantotto's Coercion" is counted as "Diabolic Intent"); if Scryfall can't be reached the names are used as written
        Dictionary<string, CardInfo> CardInfos = await GetCardInfo(FileCards.SelectMany(Cards => Cards));
        string MainName(string CardName) => CardInfos.TryGetValue(CardName, out CardInfo? Info) ? Info.Name : CardName;

        for (int FileIndex = 0; FileIndex < FileCards.Count; FileIndex++)
        {
            //Distinct() makes sure a card only counts once per decklist even if the list used two of its names
            foreach (string CardName in FileCards[FileIndex].Select(MainName).Distinct())
            {
                //update entry if it is in the dictionary, or make a new entry starting at 1 if it isn't
                //GetValueOrDefault returns 0 for a card that isn't in the dictionary yet
                DataDict[CardName] = DataDict.GetValueOrDefault(CardName) + 1;
            }
            //"|" joins partner names into one key, since card names never contain it
            string CommanderKey = string.Join("|", FileCommanders[FileIndex].Select(MainName));
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
        string[] TopCommanders = CommanderCounts.OrderByDescending(Pair => Pair.Value)
                                             .ThenByDescending(Pair => FolderWords.Count(Word => Pair.Key.Contains(Word, StringComparison.OrdinalIgnoreCase)))
                                             .Select(Pair => Pair.Key.Split('|', StringSplitOptions.RemoveEmptyEntries))
                                             .FirstOrDefault() ?? [];

        //card name -> image link for every card that has one
        Dictionary<string, string> CardImages = CardInfos.Values.Where(Info => Info.ImageUrl != null)
                                                                .DistinctBy(Info => Info.Name)
                                                                .ToDictionary(Info => Info.Name, Info => Info.ImageUrl!);

        //uses the same list of files that was read above (which includes subfolders) so the percentages line up
        return new DecklistResult(DataDict, DecklistFiles.Length, TopCommanders, CardImages);
    }

    //card names and images come from Scryfall (https://scryfall.com/docs/api)
    //Scryfall asks every app to send a User-Agent and Accept header, so they're set once on a shared HttpClient
    private static readonly HttpClient Scryfall = CreateScryfallClient();
    //card name as written in a decklist -> what Scryfall knows about it (null = Scryfall doesn't know the card)
    //card details never change, so each name is only looked up once while the site is running
    private static readonly ConcurrentDictionary<string, CardInfo?> CardInfoCache = new(StringComparer.OrdinalIgnoreCase);

    private static HttpClient CreateScryfallClient()
    {
        HttpClient Client = new() { BaseAddress = new Uri("https://api.scryfall.com/"), Timeout = TimeSpan.FromSeconds(15) };
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("DecklistChecker/1.0");
        Client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return Client;
    }

    //looks up each card name on Scryfall and returns card name as written -> Scryfall's main name and image
    //cards that can't be found (or if Scryfall can't be reached) are left out, so the page uses the name as written instead
    public static async Task<Dictionary<string, CardInfo>> GetCardInfo(IEnumerable<string> CardNames)
    {
        List<string> Names = CardNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        List<string> Missing = Names.Where(Name => !CardInfoCache.ContainsKey(Name)).ToList();

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
                //if Scryfall says no (e.g. too many requests), stop for now; the cards are tried again on the next page load
                if (!Response.IsSuccessStatusCode)
                    break;

                using JsonDocument Json = JsonDocument.Parse(await Response.Content.ReadAsStringAsync());
                //every name a returned card can be written as (main name, each face, alternate printed/flavor names) -> the card
                Dictionary<string, CardInfo> Found = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonElement Card in Json.RootElement.GetProperty("data").EnumerateArray())
                {
                    CardInfo Info = new(Card.GetProperty("name").GetString() ?? "", GetImageUrl(Card));
                    foreach (string Alias in GetNames(Card))
                        Found.TryAdd(Alias, Info);
                }
                foreach (string Name in Batch)
                {
                    if (Found.TryGetValue(Name, out CardInfo? Info))
                        CardInfoCache[Name] = Info;
                    else
                        //some alternate-name printings aren't matched by an exact name, so try Scryfall's more forgiving search
                        CardInfoCache[Name] = await FindCardByFuzzyName(Name);
                }
            }
            catch (Exception Error) when (Error is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                //Scryfall couldn't be reached or sent something unexpected; stop trying so the page still loads quickly
                //(nothing is cached, so these cards are tried again on the next page load)
                break;
            }
        }

        return Names.Where(Name => CardInfoCache.GetValueOrDefault(Name) != null)
                    .ToDictionary(Name => Name, Name => CardInfoCache[Name]!);
    }

    //Scryfall's /cards/named?fuzzy= search, used for the few names the batch lookup didn't match
    //returns null if Scryfall answered that it doesn't know the card
    private static async Task<CardInfo?> FindCardByFuzzyName(string Name)
    {
        await Task.Delay(100);
        using HttpResponseMessage Response = await Scryfall.GetAsync("cards/named?fuzzy=" + Uri.EscapeDataString(Name));
        if (Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        Response.EnsureSuccessStatusCode();
        using JsonDocument Json = JsonDocument.Parse(await Response.Content.ReadAsStringAsync());
        return new CardInfo(Json.RootElement.GetProperty("name").GetString() ?? Name, GetImageUrl(Json.RootElement));
    }

    //the Scryfall fields that hold a name a card can be listed under
    private static readonly string[] NameFields = ["name", "printed_name", "flavor_name"];

    //every name a card can be listed under in a decklist: its main name, each face of a double-faced or split card
    //(Scryfall writes these as "Front // Back" but a decklist may only list the front), and alternate-name printings
    private static IEnumerable<string> GetNames(JsonElement Card)
    {
        List<JsonElement> Parts = [Card];
        if (Card.TryGetProperty("card_faces", out JsonElement Faces))
            Parts.AddRange(Faces.EnumerateArray());
        foreach (JsonElement Part in Parts)
            foreach (string Field in NameFields)
                if (Part.TryGetProperty(Field, out JsonElement Value) && Value.GetString() is string Text)
                    yield return Text;
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
