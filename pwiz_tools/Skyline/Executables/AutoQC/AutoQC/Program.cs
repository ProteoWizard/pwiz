using System;
using System.IO;
using System.Windows.Forms;

namespace AutoQC
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
//            log4net.GlobalContext.Properties["WorkingDirectory"] = Directory.GetCurrentDirectory();
//            log4net.Config.XmlConfigurator.Configure();

            var form = new AutoQCForm();
            Application.Run(form); 
            
        }
    }
}
