using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lists;

namespace pwiz.Skyline.Model.ElementLocators
{
    public class ListRef : ElementRef
    {
        public static readonly ListRef PROTOTYPE = new ListRef();
        private ListRef() : base(DocumentRef.PROTOTYPE)
        {
        }

        public override string ElementType { get { return @"List"; } }
        protected override IEnumerable<ElementRef> EnumerateSiblings(SrmDocument document)
        {
            return document.Settings.DataSettings.Lists.Select(list => ChangeName(list.ListName));
        }

        public ListData FindList(SrmDocument document)
        {
            return document.Settings.DataSettings.FindList(Name);
        }

        public ListItemRef GetListItemRef(object pk, ListItemId? listItemId)
        {
            var listItemRef = (ListItemRef) ListItemRef.PROTOTYPE.ChangeParent(this);
            if (pk != null)
            {
                return (ListItemRef) listItemRef.ChangeName(ValueToString(pk));
            }
            if (listItemId.HasValue)
            {
                return (ListItemRef)listItemRef.ChangeName(listItemId.Value.IntValue.ToString(CultureInfo.InvariantCulture));
            }
            return (ListItemRef) listItemRef.ChangeName(string.Empty);
        }

        public static string ValueToString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            var formattable = value as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(Formats.RoundTrip, CultureInfo.InvariantCulture);
            }
            return value.ToString();
        }

        public ListItemRef GetListItemRef(ListItem listItem)
        {
            var recordData = listItem.GetRecord();
            var pk = recordData.PrimaryKeyValue;
            var listItemId = (recordData as ListItem.ExistingRecordData)?.ListItemId;
            return GetListItemRef(pk, listItemId);
        }
    }

    public class ListItemRef : ElementRef
    {
        public static readonly ListItemRef PROTOTYPE = new ListItemRef();

        private ListItemRef() : base(ListRef.PROTOTYPE)
        {
        }

        public override string ElementType { get { return @"ListItem"; } }
        protected override IEnumerable<ElementRef> EnumerateSiblings(SrmDocument document)
        {
            var listRef = (ListRef) Parent;
            var listData = listRef.FindList(document);
            if (listData == null)
            {
                return new ElementRef[0];
            }
            var pkColumn = listData.PkColumn;
            return Enumerable.Range(0, listData.RowCount).Select(row =>
                listRef.GetListItemRef(pkColumn?.GetValue(row), listData.ListItemIds[row]));
        }
    }
}
