using System.Drawing;
using System.Windows.Forms;

namespace DocumentationGenerators
{
    public class ScreenShotTaker
    {
        public Image TakeScreenShot(Form form)
        {
            var bitmap = new Bitmap(form.Width, form.Height);
            DrawForms(form, bitmap, form);
            return bitmap;
        }

        private void DrawForms(Form topLevelForm, Bitmap bitmap, Control control)
        {
            if (!control.Visible || control.Width <= 0 || control.Height <= 0)
            {
                return;
            }
            if (control is Form || control is UserControl)
            {
                var childForm = new Bitmap(control.Width, control.Height);
                control.DrawToBitmap(childForm, new Rectangle(0, 0, control.Width, control.Height));

                Point offset;
                if (control.Parent != null)
                {
                    var myPosition = control.Parent.PointToScreen(control.Location);
                    var topLevelPosition = topLevelForm.Location;
                    offset = new Point(myPosition.X - topLevelPosition.X, myPosition.Y - topLevelPosition.Y);
                }
                else
                {
                    offset = new Point(0, 0);
                }
                var g = Graphics.FromImage(bitmap);

                g.DrawImage(childForm, offset);
            }
            foreach (Control child in control.Controls)
            {
                DrawForms(topLevelForm, bitmap, child);
            }
        }
    }
}
