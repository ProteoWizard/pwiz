using ImageConverter;
using System.CommandLine;
using System.Threading.Tasks;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var inputFileOption = new Option<string>(
                name: "--input-file",
                description: "The file to read.")
            { IsRequired = true };

        var outputFileOption = new Option<string>(
                name: "--output-file",
                description: "The file to write to.")
            { IsRequired = true };

        var rootCommand = new RootCommand("Image converting command line utility");

        var convertEmfToPngCommand = new Command("convertEmfToPng", "Convert Emf to Png file")
        {
            inputFileOption,
            outputFileOption,
        };
        convertEmfToPngCommand.SetHandler( (inputFile, outputFile) =>
            {
                ConvertEmfToPng(inputFile, outputFile);
            },
            inputFileOption, outputFileOption);

        rootCommand.AddCommand(convertEmfToPngCommand);
        return await rootCommand.InvokeAsync(args);
        
    }

    public static void ConvertEmfToPng(string inputFile, string outputFile)
    {
        EmfUtilities.ConvertToPng(inputFile, outputFile);
    }
}
