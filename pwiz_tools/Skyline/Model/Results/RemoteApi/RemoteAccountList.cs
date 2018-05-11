using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public sealed class RemoteAccountList : SettingsList<RemoteAccount>
    {
        public override IEnumerable<RemoteAccount> GetDefaults(int revisionIndex)
        {
            yield break;
        }

        public override string Title { get { return Resources.ChorusAccountList_Title_Edit_Chorus_Accounts; } }

        public override string Label { get { return Resources.ChorusAccountList_Label_Chorus_Accounts; } }

        public override RemoteAccount EditItem(Control owner, RemoteAccount item, IEnumerable<RemoteAccount> existing, object tag)
        {
            using (EditChorusAccountDlg editChorusAccountDlg = new EditChorusAccountDlg(item ?? UnifiAccount.DEFAULT, existing ?? this))
            {
                if (editChorusAccountDlg.ShowDialog(owner) == DialogResult.OK)
                    return editChorusAccountDlg.GetChorusAccount();

                return null;
            }
        }

        public override RemoteAccount CopyItem(RemoteAccount item)
        {
            return item.ChangeUsername(string.Empty).ChangePassword(string.Empty);
        }

        protected override IXmlElementHelper<RemoteAccount>[] GetXmlElementHelpers()
        {
            return new IXmlElementHelper<RemoteAccount>[]
            {
                new XmlElementHelper<UnifiAccount>(),
                new XmlElementHelper<ChorusAccount>(),
            };
        }
    }
}