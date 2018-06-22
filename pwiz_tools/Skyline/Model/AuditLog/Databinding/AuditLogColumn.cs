using System.Collections.Generic;
using System.Drawing;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class AuditLogColumn : TextImageColumn
    {
        private readonly Image[] _images;

        public AuditLogColumn()
        {
            var undoRedoImage = new Bitmap(Resources.Edit_Undo);
            undoRedoImage.MakeTransparent(Color.Magenta);
            _images = new Image[] { undoRedoImage, Resources.magnifier_zoom_in };
        }

        public override bool ShouldDisplay(object cellValue, int imageIndex)
        {
            var value = cellValue as AuditLogRow.AuditLogRowText;
            if (value == null)
                return false;

            switch (imageIndex)
            {
                case 0:
                    return value.UndoAction != null;
                case 1:
                    return !string.IsNullOrEmpty(value.ExtraText);
                default:
                    return false;
            }
        }

        public override void OnClick(object cellValue, int imageIndex)
        {
            var value = cellValue as AuditLogRow.AuditLogRowText;
            if (value == null)
                return;

            switch (imageIndex)
            {
                case 0:
                    value.UndoAction();
                    break;
                case 1:
                {
                    MessageDlg.Show(DataGridView.FindForm(), value.ExtraText);
                    break;
                }
            }    
        }

        public override IList<Image> Images
        {
            get { return _images; }
        }
    }
}
