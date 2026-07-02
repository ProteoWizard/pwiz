// Stubs for Skyline types whose real implementations are excluded on net8.
// These satisfy compile-time references; runtime should never reach them.
#if !NET472
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace System.Deployment.Application
{
    // ClickOnce ApplicationDeployment isn't available on net8 — provide a stub
    // for source references; calls are also gated by #if NET472 where critical.
    public class ApplicationDeployment
    {
        public static bool IsNetworkDeployed => false;
        public static ApplicationDeployment CurrentDeployment => new ApplicationDeployment();
    }

    public class TrustNotGrantedException : Exception
    {
        public TrustNotGrantedException(string message) : base(message) { }
    }
}
#endif
