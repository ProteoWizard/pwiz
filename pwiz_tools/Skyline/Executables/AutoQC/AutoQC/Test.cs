using System;
using System.Windows.Forms;
using System.Diagnostics;

namespace AutoQC
{
    class Test
    {
             [STAThread]
             public static void Main(string[] args)
             {
                 Trace.Listeners.Clear();

                 var twtl = new TextWriterTraceListener(@"E:\EmitterChanger\log.txt")
                 {
                     Name = "TextLogger",
                     TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime
                 };

                 var ctl = new ConsoleTraceListener(false) {TraceOutputOptions = TraceOptions.DateTime};

                 Trace.Listeners.Add(twtl);
                 Trace.Listeners.Add(ctl);
                 Trace.AutoFlush = true;

                 var form = new AutoQc();
                     Application.Run(form); 
            
        }
    }
}
