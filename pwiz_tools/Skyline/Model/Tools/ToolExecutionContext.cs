using pwiz.Common.SystemUtil;
using System.Threading;

namespace pwiz.Skyline.Model.Tools
{
    public class ToolExecutionContext : Immutable
    {
        public ToolExecutionContext(IToolMacroProvider toolMacroProvider, IProgressMonitor progressMonitor, CancellationToken cancellationToken)
        {
            ToolMacroProvider = toolMacroProvider;
            ProgressMonitor = progressMonitor;
            CancellationToken = cancellationToken;
        }

        public IToolMacroProvider ToolMacroProvider { get; private set; }
        public IProgressMonitor ProgressMonitor { get; }
        public CancellationToken CancellationToken { get; }

        public ToolExecutionContext ChangeToolMacroProvider(IToolMacroProvider toolMacroProvider)
        {
            return ChangeProp(ImClone(this), im => im.ToolMacroProvider = toolMacroProvider);
        }
    }
}
