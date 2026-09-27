using System.Text.RegularExpressions;

namespace DecklistChecker;

/// <summary>A single line in a decklist: a card name and how many copies are included.</summary>
public sealed record CardEntry(int Quantity, string Name);

public sealed partial class Decklist
{
    // Cards exempt from the copy limit.
    private static readonly HashSet<string> BasicLands = new(StringComparer.OrdinalIgnoreCase)
    {
        "Plains", "Island", "Swamp", "Mountain", "Forest", "Wastes",
    };

    public List<CardEntry> MainDeck { get; } = [];
    public List<CardEntry> Sideboard { get; } = [];

    public int MainDeckCount => MainDeck.Sum(c => c.Quantity);
    public int SideboardCount => Sideboard.Sum(c => c.Quantity);

    [GeneratedRegex(@"^(?<qty>\d+)x?\s+(?<name>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LinePattern();

    /// <summary>
    /// Parses plain-text decklists in the common "4 Card Name" format.
    /// A line reading "Sideboard" (or a blank line after main-deck cards) starts the sideboard.
    /// Lines starting with "//" or "#" are treated as comments.
    /// </summary>
    public static Decklist Parse(string text)
    {
        var deck = new Decklist();
        var current = deck.MainDeck;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                if (deck.MainDeck.Count > 0)
                    current = deck.Sideboard;
                continue;
            }

            if (line.StartsWith("//") || line.StartsWith('#'))
                continue;

            if (line.TrimEnd(':').Equals("Sideboard", StringComparison.OrdinalIgnoreCase))
            {
                current = deck.Sideboard;
                continue;
            }

            var match = LinePattern().Match(line);
            if (!match.Success)
                throw new FormatException($"Could not parse decklist line: \"{line}\"");

            current.Add(new CardEntry(int.Parse(match.Groups["qty"].Value), match.Groups["name"].Value.Trim()));
        }

        return deck;
    }

    /// <summary>Checks the deck against constructed rules; returns an empty list if legal.</summary>
    public IReadOnlyList<string> Validate(int minMainDeck = 60, int maxSideboard = 15, int maxCopies = 4)
    {
        var errors = new List<string>();

        if (MainDeckCount < minMainDeck)
            errors.Add($"Main deck has {MainDeckCount} cards; minimum is {minMainDeck}.");

        if (SideboardCount > maxSideboard)
            errors.Add($"Sideboard has {SideboardCount} cards; maximum is {maxSideboard}.");

        var overLimit = MainDeck.Concat(Sideboard)
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => !BasicLands.Contains(g.Key))
            .Select(g => (Name: g.Key, Count: g.Sum(c => c.Quantity)))
            .Where(g => g.Count > maxCopies);

        foreach (var (name, count) in overLimit)
            errors.Add($"{name}: {count} copies across main deck and sideboard; maximum is {maxCopies}.");

        return errors;
    }
}
