using CommandLine;
using SkylineTool;

namespace SortProteins;

public class Options
{
    [Option("connection_name", Required = true, HelpText = "The Skyline connection name.")]
    public required string ConnectionName { get; set; }

    [Option("random", Required = false, HelpText = "Randomize the order.")]
    public bool Random { get; set; }

    [Option("reverse", Required = false, HelpText = "Reverse the order.")]
    public bool Reverse { get; set; }

    [Option("order_by", Required = false, HelpText = "Field to order by. Can be specified multiple times.")]
    public IEnumerable<string> OrderBy { get; set; } = Enumerable.Empty<string>();
}

public class Program
{
    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(Run);
    }

    private static void Run(Options options)
    {
        var remoteClient = new RemoteClient(options.ConnectionName);
        var proteinSorter = new ProteinSorter(remoteClient);
        var locators = proteinSorter.GetProteinLocators(options.OrderBy.ToArray());
        if (options.Random)
        {
            var random = new Random((int)DateTime.UtcNow.Ticks);
            locators = locators.OrderBy(x => random.NextInt64());
        }

        if (options.Reverse)
        {
            locators = locators.Reverse();
        }
        proteinSorter.SetProteinOrder(locators);
    }
}
