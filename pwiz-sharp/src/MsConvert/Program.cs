namespace Pwiz.Tools.MsConvert;

/// <summary>Entry point for the msconvert-sharp CLI.</summary>
public static class Program
{
    /// <summary>Runs msconvert-sharp with the given args; returns the process exit code (0 on full success).</summary>
    public static int Main(string[] args)
    {
        try
        {
            var config = ArgParser.Parse(args);
            var converter = new Converter(config, Console.Error);
            int converted = converter.Run();
            return converted == config.InputFiles.Count ? 0 : 1;
        }
        catch (ArgParseHelpRequested)
        {
            Console.Out.WriteLine(ArgParser.Usage());
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(ArgParser.Usage());
            return 2;
        }
    }
}
