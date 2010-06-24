//
// $Id: $
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

    public class BreadCrumbControl : UserControl
    {
        private BreadCrumbToolStrip breadCrumbToolStrip;
        private BindingList<object> breadCrumbs;

        public IList<object> BreadCrumbs { get { return breadCrumbs; } }

        public event EventHandler BreadCrumbClicked;

        public BreadCrumbControl ()
        {
            breadCrumbToolStrip = new BreadCrumbToolStrip()
            {
                RightToLeft = RightToLeft.Yes,
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden
            };
            //breadCrumbToolStrip.OverflowButton.Alignment = ToolStripItemAlignment.Right;

            Controls.Add(breadCrumbToolStrip);

            breadCrumbs = new BindingList<object>()
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
                var reversedSequence = breadCrumbs.Reverse();


                object lastBreadCrumb = reversedSequence.First();
                if (lastBreadCrumb is ToolStripItem)
                {
                    breadCrumbToolStrip.Items.Add(lastBreadCrumb as ToolStripItem);
                }
                else if (lastBreadCrumb is IList<ToolStripItem>)
                {
                    breadCrumbToolStrip.Items.AddRange((lastBreadCrumb as IList<ToolStripItem>).Reverse().ToArray());
                }
                else
                {
                    var lastBreadCrumbLabel = new ToolStripLabel()
                    {
                        Text = lastBreadCrumb.ToString(),
                        TextAlign = ContentAlignment.MiddleLeft,
                        AutoSize = true,
                        Tag = lastBreadCrumb
                    };

                    breadCrumbToolStrip.Items.Add(lastBreadCrumbLabel);
                }

                foreach(var breadCrumb in reversedSequence.Skip(1))
                {
                    var breadCrumbSeparator = new ToolStripLabel()
                    {
                        Text = ">",
                        TextAlign = ContentAlignment.MiddleLeft,
                        AutoSize = true
                    };
                    breadCrumbToolStrip.Items.Add(breadCrumbSeparator);

                    var breadCrumbLinkLabel = new ToolStripLabel()
                    {
                        Text = breadCrumb.ToString(),
                        TextAlign = ContentAlignment.MiddleLeft,
                        IsLink = true,
                        LinkBehavior = LinkBehavior.AlwaysUnderline,
                        AutoSize = true,
                        BackColor = Color.Red,
                        Tag = breadCrumb
                    };
                    breadCrumbLinkLabel.Click += new EventHandler(itemLinkLabel_LinkClicked);

                    breadCrumbToolStrip.Items.Add(breadCrumbLinkLabel);

                    if (breadCrumb is ToolStripItem)
                    {
                        breadCrumbLinkLabel.Text = "(x)";
                        breadCrumbToolStrip.Items.Add(breadCrumb as ToolStripItem);
                    }
                    else if (breadCrumb is IList<ToolStripItem>)
                    {
                        breadCrumbLinkLabel.Text = "(x)";
                        breadCrumbToolStrip.Items.AddRange((breadCrumb as IList<ToolStripItem>).Reverse().ToArray());
                    }
                    else
                    {
                        breadCrumbLinkLabel.Text = breadCrumb.ToString();
                    }
                }
            }

            ResumeLayout();
            Refresh();
        }

        protected void OnLinkClicked (object sender)
        {
            if (BreadCrumbClicked != null)
                BreadCrumbClicked(sender, EventArgs.Empty);
        }

        void itemLinkLabel_LinkClicked (object sender, EventArgs e)
        {
            OnLinkClicked((sender as ToolStripItem).Tag);
        }
    }
}
