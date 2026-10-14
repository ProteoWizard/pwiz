namespace Pwiz.Tools.MsDiff;

/// <summary>Entry point for the msdiff-sharp CLI.</summary>
public static class Program
{
    /// <summary>
    /// Runs msdiff-sharp. Returns 0 when the two files match, 1 when they differ or the run
    /// failed, and 2 for a bad command line - the first two matching cpp msdiff, whose
    /// <c>main</c> returns the diff itself and 1 from its catch blocks.
    /// </summary>
    public static int Main(string[] args)
    {
        // Hook the vendor SDK on-demand resolver before any Reader_* is touched, exactly as
        // msconvert does; without it a vendor input fails to load its SDK.
        Pwiz.Vendor.Common.VendorSdkLoader.RegisterAssemblyResolver();
        try
        {
            var config = ArgParser.Parse(args);
            return new Differ().Run(config, Console.Out);
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
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
