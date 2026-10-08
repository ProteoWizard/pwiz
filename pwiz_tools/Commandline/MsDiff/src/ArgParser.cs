using Pwiz.Data.Common.Diff;

namespace Pwiz.Tools.MsDiff;

/// <summary>Thrown when the user asked for help rather than a comparison.</summary>
internal sealed class ArgParseHelpRequested : Exception;

/// <summary>Parsed msdiff-sharp command line.</summary>
internal sealed class Config
{
    public string FileA { get; set; } = string.Empty;
    public string FileB { get; set; } = string.Empty;
    public DiffConfig DiffConfig { get; } = new();
}

/// <summary>
/// Command-line parsing for msdiff-sharp, matching cpp <c>msdiff</c>'s boost::program_options
/// surface: two positional filenames plus <c>-p</c> and <c>-i</c>.
/// </summary>
internal static class ArgParser
{
    public static string Usage() =>
        """
        Usage: msdiff [options] filename1 filename2
        Compare two mass spec data files.

        Options:
          -p [ --precision ] arg (=1e-06)  : set floating point precision for comparing binary data
          -i [ --ignore ]                  : ignore metadata (compare scan binary data and
                                             important scan metadata only)

        Questions, comments, and bug reports:
        https://github.com/ProteoWizard
        support@proteowizard.org
        """;

    public static Config Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var config = new Config();
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "-h" or "--help" or "-?":
                    throw new ArgParseHelpRequested();

                case "-i" or "--ignore":
                    // cpp declares this zero_tokens, so it never consumes a value.
                    config.DiffConfig.IgnoreMetadata = true;
                    break;

                case "-p" or "--precision":
                    config.DiffConfig.Precision = ParsePrecision(Next(args, ref i, a));
                    break;

                default:
                    if (a.StartsWith("--precision=", StringComparison.Ordinal))
                        config.DiffConfig.Precision = ParsePrecision(a["--precision=".Length..]);
                    else if (a.StartsWith('-') && a.Length > 1)
                        throw new ArgumentException($"unrecognized option '{a}'");
                    else
                        positional.Add(a);
                    break;
            }
        }

        // cpp throws the usage string when it does not get exactly two filenames.
        if (positional.Count != 2)
            throw new ArgumentException(
                $"expected exactly 2 filenames, got {positional.Count}");

        config.FileA = positional[0];
        config.FileB = positional[1];
        return config;
    }

    private static string Next(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"option '{option}' requires a value");
        return args[++i];
    }

    private static double ParsePrecision(string s)
    {
        // Invariant culture on purpose: "1e-5" must parse the same wherever this runs, and the
        // container sets no particular culture.
        if (!double.TryParse(s, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out double v))
            throw new ArgumentException($"could not parse precision '{s}'");
        return v;
    }
}
