// Based on https://www.codeproject.com/Articles/2369/ImageListBox-exposing-localizable-custom-object-co
// Which is Copyright (C) 2002 by Alexander Yakovlev. All Rights Reserved.
// with no particular license specified.

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// List box with images that supports design-time editing
    /// </summary>
    [DefaultProperty("Items")]
    [DefaultEvent("SelectedIndexChanged")]
    public class ImageListBox : ListBox
    {
        #region ImageListBoxItemCollection class...

        /// <summary>
        /// The list box's items collection class
        /// </summary>
        public class ImageListBoxItemCollection : IList
        {
            ImageListBox owner;

            public ImageListBoxItemCollection(ImageListBox owner)
            {
                this.owner = owner;
            }

            #region ICollection implemented members...

            void ICollection.CopyTo(Array array, int index) 
            {
                for (IEnumerator e = GetEnumerator(); e.MoveNext();)
                    array.SetValue(e.Current, index++);
            }

            bool ICollection.IsSynchronized 
            {
                get { return false; }
            }

            object ICollection.SyncRoot 
            {
                get { return this; }
            }

            #endregion

            #region IList implemented members...

            object IList.this[int index] 
            {
                get { return this[index]; }
                set { this[index] = (ImageListBoxItem)value; }
            }

            bool IList.Contains(object item)
            {
                throw new NotSupportedException();
            }

            int IList.Add(object item)
            {
                return Add((ImageListBoxItem)item);
            }

            bool IList.IsFixedSize 
            {
                get { return false; }
            }

            int IList.IndexOf(object item)
            {
                throw new NotSupportedException();
            }

            void IList.Insert(int index, object item)
            {
                Insert(index, (ImageListBoxItem)item);
            }

            void IList.Remove(object item)
            {
                throw new NotSupportedException();
            }

            void IList.RemoveAt(int index)
            {
                RemoveAt(index);
            }

            #endregion

            [Browsable(false)]
            public int Count 
            {
                get { return owner.DoGetItemCount(); }
            }

            public bool IsReadOnly 
            {
                get { return false; }
            }

            public ImageListBoxItem this[int index]
            {
                get { return owner.DoGetElement(index); }
                set { owner.DoSetElement(index, value); }
            }

            public IEnumerator GetEnumerator() 
            {
                return owner.DoGetEnumerator(); 
            }

            public bool Contains(object item)
            {
                throw new NotSupportedException();
            }

            public int IndexOf(object item)
            {
                throw new NotSupportedException();
            }

            public void Remove(ImageListBoxItem item)
            {
                throw new NotSupportedException();
            }

            public void Insert(int index, ImageListBoxItem item)
            {
                owner.DoInsertItem(index, item);
            }

            public int Add(ImageListBoxItem item)
            {
                return owner.DoInsertItem(Count, item);
            }

            public void AddRange(ImageListBoxItem[] items)
            {
                for(IEnumerator e = items.GetEnumerator(); e.MoveNext();)
                    owner.DoInsertItem(Count, (ImageListBoxItem)e.Current);
            }

            public void Clear()
            {
                owner.DoClear();
            }

            public void RemoveAt(int index)
            {
                owner.DoRemoveItem(index);
            }
        }

        #endregion

        #region Methods to access base class items...

        private void DoSetElement(int index, ImageListBoxItem value)
        {
            base.Items[index] = value;
        }

        private ImageListBoxItem DoGetElement(int index)
        {
            return (ImageListBoxItem)base.Items[index];
        }

        private IEnumerator DoGetEnumerator()
        {
            return base.Items.GetEnumerator();
        }

        private int DoGetItemCount()
        {
            return base.Items.Count;
        }

        private int DoInsertItem(int index, ImageListBoxItem item)
        {
            item.imageList = imageList;
            item.itemIndex = index;
            base.Items.Insert(index, item);
            return index;
        }

        private void DoRemoveItem(int index)
        {
            base.Items.RemoveAt(index);
        }

        private void DoClear()
        {
            base.Items.Clear();
        }

        #endregion

        private ImageList imageList;
        ImageListBoxItemCollection listItems;

        public ImageListBox()
        {
            // Set owner draw mode
            base.DrawMode = DrawMode.OwnerDrawFixed;
            listItems = new ImageListBoxItemCollection(this);
        }

        /// <summary>
        /// Hides the parent DrawMode property from property browser
        /// </summary>
        [Browsable(false)]
        override public DrawMode DrawMode
        {
            get { return base.DrawMode; }
            set { }
        }

        /// <summary>
        /// The ImageList control from which this listbox takes the images
        /// </summary>
        [Category("Behavior")]  
        [Description("The ImageList control from which this list box takes the images")]
        [DefaultValue(null)]
        public ImageList ImageList
        {
            get { return imageList; }
            set { 
                imageList = value; 
                // Update the imageList field for the items
                for(int i = 0; i < listItems.Count; i++) 
                {
                    listItems[i].imageList = imageList;
                }
                // Invalidate the control
                Invalidate();
            }
        }

        /// <summary>
        /// The items in the list box
        /// </summary>
        [Category("Behavior")]  
        [Description("The items in the list box")]
        [Localizable(true)]
        [MergableProperty(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        new public ImageListBoxItemCollection Items
        {
            get { return listItems; }
        }

        /// <summary>
        /// Overrides parent OnDrawItem method to perform custom painting
        /// </summary>
        /// <param name="pe"></param>
        protected override void OnDrawItem(DrawItemEventArgs pe)
        {
            pe.DrawBackground();
            pe.DrawFocusRectangle();
            Rectangle bounds = pe.Bounds;
            // Check whether the index is valid
            if(pe.Index >= 0 && pe.Index < base.Items.Count) 
            {
                ImageListBoxItem item = (ImageListBoxItem)base.Items[pe.Index];
                int iOffset = 0;
                // If the image list is present and the image index is set, draw the image
                if(imageList != null)
                {
                    if (item.ImageIndex != -1 && item.ImageIndex < imageList.Images.Count) 
                    {
                        imageList.Draw(pe.Graphics, bounds.Left, bounds.Top, item.ImageIndex); 
                    }
                    iOffset += imageList.ImageSize.Width;
                }
                // Draw item text
                pe.Graphics.DrawString(item.Text, pe.Font, new SolidBrush(pe.ForeColor), 
                    bounds.Left + iOffset, bounds.Top);
            }
            base.OnDrawItem(pe);
        }
    }
    
    /// <summary>
    /// ImageListBox item class
    /// </summary>
    [ToolboxItem(false)]
    [DesignTimeVisible(false)]
    [DefaultProperty("Text")]
    public class ImageListBoxItem : Component, ICloneable
    {
        private string text;
        private int imageIndex;
        public ImageList imageList;
        public int itemIndex = -1;

        /// <summary>
        /// Item index. Used by collection editor
        /// </summary>
        [Browsable(false)]
        public int Index
        {
            get { return itemIndex; }
        }

        /// <summary>
        /// The image list for this item. Used by UI editor
        /// </summary>
        [Browsable(false)]
        public ImageList ImageList
        {
            get { return imageList; }
        }

        /// <summary>
        /// The item's text
        /// </summary>
        [Localizable(true)]
        public string Text
        {
            get { return text; }
            set { text = value; }
        }
        
        /// <summary>
        /// The item's image index
        /// </summary>
        [DefaultValue(-1)]
        [Localizable(true)]
        [TypeConverter(typeof(ImageIndexConverter))]
        [Editor("System.Windows.Forms.Design.ImageIndexEditor", typeof(System.Drawing.Design.UITypeEditor))]
        public int ImageIndex
        {
            get { return imageIndex; }
            set { imageIndex = value; }
        }

        /// <summary>
        /// Item constructors
        /// </summary>
        public ImageListBoxItem(string Text, int ImageIndex)
        {
            text = Text;
            imageIndex = ImageIndex;
        }

        public ImageListBoxItem(string Text) : this(Text, -1)
        {
        }

        public ImageListBoxItem() : this("")
        {
        }

        #region ICloneable implemented members...

        public object Clone() 
        {
            return new ImageListBoxItem(text, imageIndex);
        }

        #endregion

        /// <summary>
        /// Converts the item to string representation. Needed for property editor
        /// </summary>
        /// <returns>String representation of the item</returns>
        public override string ToString()
        {
            return @"Item: {" + text + @"}";
        }
    }
    
}
