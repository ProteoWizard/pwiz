using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class MsGraphExtension : UserControl
    {
        public bool PropertiesVisible
        {
            get { return !splitContainer1.Panel2Collapsed; }
            set { splitContainer1.Panel2Collapsed = !value; }
        }
        public string PropertySheetVisibilityPropName { get; set; }

        public MSGraphControl Graph => graphControl;

        public PropertyGrid PropertiesSheet => spectrumInfoSheet;

        public SplitContainer Splitter => splitContainer1;

        public event EventHandler<EventArgs> PropertiesSheetVisibilityChanged = delegate { };

        public MsGraphExtension()
        {
            InitializeComponent();

            Splitter.MouseDown += splitContainer1_MouseDown;
            Splitter.MouseUp += splitContainer1_MouseUp;
            PropertiesSheet.PropertySortChanged += PropertiesSheet_PropertySortChanged;
            var toolStrip = spectrumInfoSheet.Controls.OfType<ToolStrip>().First();

            CloseButton = new ToolStripButton()
            {
                Alignment = ToolStripItemAlignment.Right,
                Image = Resources.Close,
                ImageTransparentColor = Color.Magenta,
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                Tag = @"Close button"
            };
            var resources = new ComponentResourceManager(typeof(MsGraphExtension));
            resources.ApplyResources(CloseButton, "closeButton");
            CloseButton.Click += closeButton_Click;
            toolStrip.Items.Add(CloseButton);
            
        }

        public void RestorePropertiesSheet()
        {
            if (Settings.Default[PropertySheetVisibilityPropName] is bool visibilityProp)
                PropertiesVisible = visibilityProp;
            else
                PropertiesVisible = false;

            if (Settings.Default.ViewLibraryPropertiesSorted)
                PropertiesSheet.PropertySort = PropertySort.Alphabetical;
            if (PropertiesVisible)
                RestoreSplitterWidthSetting();
        }

        public void SetPropertiesObject(GlobalizedObject spectrumProperties)
        {
            spectrumInfoSheet.SelectedObject = spectrumProperties;
        }

        /// <summary>
        /// Replaces the property sheet's selected object while preserving which expandable nodes
        /// (categories such as "Other Metadata", and sub-objects such as Instrument) the user had
        /// expanded, so navigating between spectra does not collapse them.
        /// </summary>
        public void SetSelectedObjectPreservingExpansion(object propertiesObject)
        {
            var expanded = GetExpandedKeys();
            spectrumInfoSheet.SelectedObject = propertiesObject;
            if (expanded.Count > 0)
                RestoreExpandedKeys(expanded);
        }

        private HashSet<string> GetExpandedKeys()
        {
            var expanded = new HashSet<string>();
            foreach (var item in EnumerateGridItems())
            {
                if (item.Expandable && item.Expanded)
                    expanded.Add(GridItemKey(item));
            }
            return expanded;
        }

        private void RestoreExpandedKeys(HashSet<string> expanded)
        {
            foreach (var item in EnumerateGridItems())
            {
                if (item.Expandable && !item.Expanded && expanded.Contains(GridItemKey(item)))
                    item.Expanded = true;
            }
        }

        private static string GridItemKey(GridItem item)
        {
            return item.PropertyDescriptor != null ? item.PropertyDescriptor.Name : item.Label;
        }

        private IEnumerable<GridItem> EnumerateGridItems()
        {
            var selected = spectrumInfoSheet.SelectedGridItem;
            if (selected == null)
                yield break;
            var root = selected;
            while (root.Parent != null)
                root = root.Parent;
            foreach (var item in EnumerateGridItems(root))
                yield return item;
        }

        private static IEnumerable<GridItem> EnumerateGridItems(GridItem parent)
        {
            foreach (GridItem child in parent.GridItems)
            {
                yield return child;
                foreach (var descendant in EnumerateGridItems(child))
                    yield return descendant;
            }
        }
        public void ShowPropertiesSheet(bool show)
        {
            if (!show && PropertiesVisible)
                SaveSplitterWidthSetting();
            PropertiesVisible = show;
            Settings.Default[PropertySheetVisibilityPropName] = show;
            if (show)
            {
                RestoreSplitterWidthSetting();
                if (Settings.Default.ViewLibraryPropertiesSorted)
                    PropertiesSheet.PropertySort = PropertySort.Alphabetical;
            }
        }

        public void TogglePropertiesSheet()
        {
            ShowPropertiesSheet(!PropertiesVisible);
        }

        public void SaveSplitterWidthSetting()
        {
            if (Width > 0)
                Settings.Default.ViewLibrarySplitPropsDist = 1 - 1.0f * Splitter.SplitterDistance / Width;
        }

        public void RestoreSplitterWidthSetting()
        {
            if (Width > 0)
            {
                if (Settings.Default.ViewLibrarySplitPropsDist > 0 && Settings.Default.ViewLibrarySplitPropsDist < 1)
                    Splitter.SplitterDistance = (int)(Width * (1 - Settings.Default.ViewLibrarySplitPropsDist));
                else
                    Splitter.SplitterDistance = (int)(Width * 0.66);

            }
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            ShowPropertiesSheet(false);
            PropertiesSheetVisibilityChanged(this, EventArgs.Empty);
        }

        private void PropertiesSheet_PropertySortChanged(object sender, EventArgs e)
        {
            Settings.Default.ViewLibraryPropertiesSorted = (PropertiesSheet.PropertySort == PropertySort.Alphabetical);
        }

 
        private Control _focusedControl;
        private void splitContainer1_MouseDown(object sender, MouseEventArgs e)
        {
            Control c = this;
            while(c != null && !(c is FormEx))
                c = c.Parent;
            var parent = c as FormEx;
            if (parent != null)
                _focusedControl = FormEx.GetFocused(parent.Controls);
            else
                _focusedControl = Parent;
        }

        private void splitContainer1_MouseUp(object sender, MouseEventArgs e)
        {
            if (Width > 0)
                Settings.Default.ViewLibrarySplitPropsDist = 1 - 1.0f * e.X / Width;

            _focusedControl?.Focus();
            _focusedControl = null;
        }

        #region Test Support
        public ToolStripButton CloseButton { get; private set; }
        #endregion

    }
}
