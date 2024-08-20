using CommandLine;

namespace ToolServiceCmd
{
    public abstract class BaseCommand
    {
        [Option(Required = true, HelpText = "Connection name from Skyline")]
        public string ConnectionName { get; set; }

        public abstract int PerformCommand();

        protected ToolServiceClient GetSkylineToolClient()
        {
            return new ToolServiceClient(ConnectionName);
        }
    }
}
