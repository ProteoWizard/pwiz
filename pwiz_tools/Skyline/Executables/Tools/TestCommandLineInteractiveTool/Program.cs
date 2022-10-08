using SkylineTool;
using System;

namespace TestCommandLineInteractiveTool
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: {0} <connection> (OutputProcessIds|MonitorSelection|SetNoteOnSelectedNode)...");
                return -1;
            }
            var toolConnection = args[0];
            // Open connection to Skyline.
            using (var toolClient = new SkylineToolClient(toolConnection, "Test Interactive Tool")) // Not L10N
            {
                for (int i = 1; i < args.Length; i++)
                {
                    string commandName = args[i];
                    if (commandName == "OutputProcessIds")
                    {
                        new OutputProcessIds(toolClient).RunCommand();
                    }
                    else if (commandName == "MonitorSelection")
                    {
                        new MonitorSelection(toolClient).RunCommand();
                    }
                    else if (commandName == "SetNoteOnSelectedNode")
                    {
                        new SetNoteOnSelectedNode(toolClient).RunCommand();
                    }
                    else if (commandName == "DeleteSelectedNode")
                    {
                        new DeleteSelectedNode(toolClient).RunCommand();
                    }
                    else
                    {
                        Console.Error.WriteLine("Unknown command: {0}", commandName);
                    }
                }
            }

            return 0;
        }
    }
}
