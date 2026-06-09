namespace Pwiz.Tools.MsConvert;

/// <summary>Entry point for the msconvert-sharp CLI.</summary>
public static class Program
{
    /// <summary>Runs msconvert-sharp with the given args; returns the process exit code (0 on full success).</summary>
    public static int Main(string[] args)
    {
        // Hook vendor SDK on-demand resolver before any Reader_* is touched. See
        // Pwiz.Vendor.Common.VendorSdkLoader.
        Pwiz.Vendor.Common.VendorSdkLoader.RegisterAssemblyResolver();
        try
        {
            var config = ArgParser.Parse(args);
            // Rebuild the command-line args verbatim for the MS_command_line_parameters cvParam
            // we'll stamp onto the output's first DataProcessing. Mirrors cpp msconvert.cpp:1171-1178:
            // quote any arg with whitespace / separator characters so the recorded string is
            // re-parseable.
            config.CommandLineParameters = BuildCommandLineString(args);
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

    private static string BuildCommandLineString(string[] args)
    {
        // Quote any arg containing whitespace or the characters cpp considers split-significant
        // (space, tab, comma, semicolon, ampersand). Mirrors cpp msconvert.cpp:1175.
        char[] needsQuotes = { ' ', '\t', ',', ';', '&', '=' };
        var parts = new System.Collections.Generic.List<string>(args.Length);
        foreach (var a in args)
        {
            parts.Add(a.IndexOfAny(needsQuotes) >= 0 ? "\"" + a + "\"" : a);
        }
        return string.Join(' ', parts);
    }
}
