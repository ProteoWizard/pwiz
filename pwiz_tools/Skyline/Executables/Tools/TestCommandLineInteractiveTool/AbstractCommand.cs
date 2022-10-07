using SkylineTool;

namespace TestCommandLineInteractiveTool
{
    public abstract class AbstractCommand
    {
        protected AbstractCommand(SkylineToolClient skylineToolClient)
        {
            SkylineToolClient = skylineToolClient;
        }

        public SkylineToolClient SkylineToolClient { get; }

        public abstract void RunCommand();
    }
}
