using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.ui.Util
{
    public class LongWaitAction
    {
        private bool _cancelled;
        public LongWaitAction(Action action)
        {
            Action = action;
        }

        public Action Action { get; private set; }

        
    }
}
