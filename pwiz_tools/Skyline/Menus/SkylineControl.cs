using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;

namespace pwiz.Skyline.Menus
{
    public class SkylineControl : UserControl
    {
        public SkylineControl()
        {
        }

        public SkylineControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;
        }

        public SkylineWindow SkylineWindow { get; private set; }
        public SrmDocument DocumentUI
        {
            get { return SkylineWindow?.DocumentUI; }
        }

        public SrmDocument Document
        {
            get { return SkylineWindow?.Document; }
        }

        public void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act, Func<SrmDocumentPair, AuditLogEntry> logFunc)
        {
            SkylineWindow.ModifyDocument(description, null, act, null, null, logFunc);
        }

        public IdentityPath SelectedPath
        {
            get { return SkylineWindow?.SelectedPath; }
            set { SkylineWindow.SelectedPath = value; }
        }
    }
}
