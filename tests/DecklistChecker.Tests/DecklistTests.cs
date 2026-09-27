namespace DecklistChecker.Tests;

public class DecklistTests
{
    [Fact]
    public void Parses_main_deck_and_sideboard()
    {
        var deck = Decklist.Parse("""
            4 Lightning Bolt
            20 Mountain

            Sideboard
            2x Pyroblast
            """);

        Assert.Equal(24, deck.MainDeckCount);
        Assert.Equal(2, deck.SideboardCount);
        Assert.Equal(new CardEntry(2, "Pyroblast"), deck.Sideboard[0]);
    }

    [Fact]
    public void Throws_on_malformed_line()
    {
        Assert.Throws<FormatException>(() => Decklist.Parse("Lightning Bolt"));
    }

    [Fact]
    public void Legal_deck_has_no_errors()
    {
        var deck = Decklist.Parse("4 Lightning Bolt\n56 Mountain");

        Assert.Empty(deck.Validate());
    }

    [Fact]
    public void Flags_too_few_cards_and_too_many_copies()
    {
        var deck = Decklist.Parse("4 Lightning Bolt\n\nSideboard\n1 Lightning Bolt");

        Assert.Equal(2, deck.Validate().Count);
    }
}
