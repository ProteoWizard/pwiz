using System;
using System.Diagnostics;
using SkylineTool;

namespace TestCommandLineInteractiveTool
{
    public class OutputProcessIds : AbstractCommand
    {
        public OutputProcessIds(SkylineToolClient skylineToolClient) : base(skylineToolClient)
        {
        }

        public override void RunCommand()
        {
            Console.Out.WriteLine("{0} process id: {1} Skyline process id: {2}", typeof(OutputProcessIds).Assembly.GetName().Name, Process.GetCurrentProcess().Id, SkylineToolClient.GetProcessId());
        }
    }
}
