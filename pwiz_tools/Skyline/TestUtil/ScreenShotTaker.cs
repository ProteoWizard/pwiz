using System.Drawing;
using System.Windows.Forms;

namespace pwiz.SkylineTestUtil
{
    public class ScreenShotTaker
    {
        public Image TakeScreenShot(Control form)
        {
            var bitmap = new Bitmap(form.Width, form.Height);
            DrawForms(form, bitmap, form);
            return bitmap;
        }

        private void DrawForms(Control topLevelForm, Bitmap bitmap, Control control)
        {
            if (!control.Visible || control.Width <= 0 || control.Height <= 0)
            {
                return;
            }
            if (control is Form || control is UserControl || control == topLevelForm)
            {
                Point offset = new Point(0, 0);
                if (control != topLevelForm && control.Parent != null)
                {
                    var myPosition = GetScreenLocation(control);
                    var topLevelPosition = GetScreenLocation(topLevelForm);
                    offset = new Point(myPosition.X - topLevelPosition.X, myPosition.Y - topLevelPosition.Y);
                }

                var childForm = new Bitmap(control.Width, control.Height);
                control.DrawToBitmap(childForm, new Rectangle(0, 0, control.Width, control.Height));
                var g = Graphics.FromImage(bitmap);

                g.DrawImage(childForm, offset);
            }
            foreach (Control child in control.Controls)
            {
                DrawForms(topLevelForm, bitmap, child);
            }
        }

        private Point GetScreenLocation(Control control)
        {
            if (control.Parent != null)
            {
                return control.Parent.PointToScreen(control.Location);
            }
            return control.Location;
        }
    }
}
