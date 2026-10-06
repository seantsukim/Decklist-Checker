using System;
using System.IO;

namespace DecklistChecker;

public class Program
{
    public string BaseDirectory = AppContext.BaseDirectory;
    private static void Main(string[] args)
    {
        Console.WriteLine(BaseDirectory.ToString());
    }
}
