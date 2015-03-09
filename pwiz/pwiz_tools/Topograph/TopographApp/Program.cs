using System;
using System.Threading;
using System.Windows.Forms;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Forms;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui
{
    static class Program
    {
        public const string AppName = "Topograph";
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadExceptionEventHandler;
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TopographForm());
        }
        public static void Application_ThreadExceptionEventHandler(Object sender, ThreadExceptionEventArgs e)
        {
            ErrorHandler.LogException("Topograph", "Unhandled exception", e.Exception);
        }
        public static T FindOpenForm<T>() where T : Form
        {
            foreach (var form in Application.OpenForms)
            {
                if (form is T)
                {
                    return (T) form;
                }
            }
            return null;
        }
        public static T FindOpenEntityForm<T>(EntityModel entity) where T : EntityModelForm
        {
            foreach (var form in Application.OpenForms)
            {
                T entityModelForm = form as T;
                if (entityModelForm == null)
                {
                    continue;
                }
                if (Equals(entityModelForm.EntityModel, entity))
                {
                    return entityModelForm;
                }
            }
            return null;
        }
    }
}
