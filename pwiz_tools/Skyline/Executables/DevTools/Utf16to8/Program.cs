using System.Text;

if (args.Length < 2)
{
    Console.WriteLine("Usage: Utf16to8 <sourceFile> <destinationFile>");
    return;
}

string sourceFile = args[0];
string destinationFile = args[1];

try
{
    // Read all text from the source file with UTF-16 encoding (default for StreamReader with UTF-16 files)
    string content = File.ReadAllText(sourceFile, Encoding.Unicode)
        // Replace possible uppercase or lowercase specifiers in XML
        // Unsure why DigitalRune would produce both variants, but they have been seen
        .Replace("UTF-16", "UTF-8")
        .Replace("utf-16", "utf-8");

    // Write the content to the destination file in UTF-8 encoding with BOM
    File.WriteAllText(destinationFile, content, new UTF8Encoding(true));

    Console.WriteLine($"Converted {Path.GetFileName(sourceFile)} to UTF-8 with BOM.");
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
}