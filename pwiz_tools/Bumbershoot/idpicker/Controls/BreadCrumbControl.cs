//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IDPicker.Controls
{
    internal class BreadCrumbToolStrip : ToolStrip
    {
        /*protected ContentAlignment RtlTranslateAlignment (ContentAlignment align)
        {
            return ContentAlignment.MiddleRight;
        }

        protected HorizontalAlignment RtlTranslateAlignment (HorizontalAlignment align)
        {
            return HorizontalAlignment.Right;
        }

        protected LeftRightAlignment RtlTranslateAlignment (LeftRightAlignment align)
        {
            return LeftRightAlignment.Right;
        }

        protected internal ContentAlignment RtlTranslateContent (ContentAlignment align)
        {
            return ContentAlignment.MiddleRight;
        }

        protected HorizontalAlignment RtlTranslateHorizontal (HorizontalAlignment align)
        {
            return HorizontalAlignment.Right;
        }

        protected LeftRightAlignment RtlTranslateLeftRight (LeftRightAlignment align)
        {
            return LeftRightAlignment.Right;
        }*/
    }

    public class BreadCrumb
    {
        public BreadCrumb (string text, object tag)
        {
            Text = text;
            Tag = tag;
        }

        public string Text { get; private set; }
        public object Tag { get; private set; }
    }

    public class BreadCrumbClickedEventArgs : EventArgs
    {
        internal BreadCrumbClickedEventArgs (BreadCrumb breadCrumb) { BreadCrumb = breadCrumb; }
        public BreadCrumb BreadCrumb { get; private set; }
    }

    public class BreadCrumbControl : UserControl
    {
        private BreadCrumbToolStrip breadCrumbToolStrip;
        private BindingList<BreadCrumb> breadCrumbs;

        public IList<BreadCrumb> BreadCrumbs { get { return breadCrumbs; } }

        public event EventHandler<BreadCrumbClickedEventArgs> BreadCrumbClicked;

        public BreadCrumbControl ()
        {
            breadCrumbToolStrip = new BreadCrumbToolStrip()
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden
            };
            breadCrumbToolStrip.OverflowButton.Alignment = ToolStripItemAlignment.Left;

            Controls.Add(breadCrumbToolStrip);

            breadCrumbs = new BindingList<BreadCrumb>()
            {
                RaiseListChangedEvents = true
            };

            breadCrumbs.ListChanged += new ListChangedEventHandler(breadCrumbs_ListChanged);
        }

        void breadCrumbs_ListChanged (object sender, ListChangedEventArgs e)
        {
            SuspendLayout();
            breadCrumbToolStrip.Items.Clear();

            if (breadCrumbs.Count > 0)
            {
                foreach (var breadCrumb in breadCrumbs)
                {
                    ToolStripItemOverflow overflow = breadCrumb == breadCrumbs.Last() ? ToolStripItemOverflow.Never
                                                                                      : ToolStripItemOverflow.AsNeeded;
                    var breadCrumbSeparator = new ToolStripLabel()
                    {
                        Text = ">",
                        TextAlign = ContentAlignment.MiddleLeft,
                        AutoSize = true,
                        Overflow = overflow,
                    };
                    breadCrumbToolStrip.Items.Add(breadCrumbSeparator);

                    var breadCrumbLabel = new ToolStripStatusLabel()
                    {
                        Text = breadCrumb.Text,
                        TextAlign = ContentAlignment.MiddleRight,
                        Overflow = overflow,
                        Tag = breadCrumb.Tag
                    };
                    breadCrumbToolStrip.Items.Add(breadCrumbLabel);

                    var breadCrumbLinkLabel = new ToolStripLabel()
                    {
                        Text = "(x)",
                        TextAlign = ContentAlignment.MiddleLeft,
                        IsLink = true,
                        LinkBehavior = LinkBehavior.AlwaysUnderline,
                        AutoSize = true,
                        BackColor = Color.Red,
                        Overflow = overflow,
                        Tag = breadCrumb
                    };
                    breadCrumbLinkLabel.Click += new EventHandler(itemLinkLabel_LinkClicked);
                    breadCrumbToolStrip.Items.Add(breadCrumbLinkLabel);
                }
            }

            ResumeLayout();
            Refresh();
        }

        protected void OnLinkClicked (BreadCrumb sender)
        {
            if (BreadCrumbClicked != null)
                BreadCrumbClicked(sender, new BreadCrumbClickedEventArgs(sender));
        }

        void itemLinkLabel_LinkClicked (object sender, EventArgs e)
        {
            OnLinkClicked((sender as ToolStripItem).Tag as BreadCrumb);
        }
    }
}
