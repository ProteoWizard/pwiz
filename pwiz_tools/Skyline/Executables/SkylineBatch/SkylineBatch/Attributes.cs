using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace SkylineBatch
{
    /// <summary>
    ///
    /// The classes in Attributes.cs were modified from solutions at:
    ///     https://www.codeproject.com/script/Articles/ViewDownloads.aspx?aid=2138
    /// and:
    ///     TODO (ALI): CHeck if this is a cleaner solution for name/description as well
    ///     https://www.infragistics.com/community/blogs/b/blagunas/posts/localize-property-names-descriptions-and-categories-for-the-xampropertygrid
    /// 
    /// </summary>

    [AttributeUsage(AttributeTargets.Property, AllowMultiple=false, Inherited=true)]
    public class GlobalizedPropertyAttribute : Attribute
    {
        private String resourceName = "";
        private String resourceDescription = "";
        private String resourceTable = "";

        public GlobalizedPropertyAttribute(String name)
        {
            resourceName = name;
        }

        public String Name
        {
            get {  return resourceName;  }
            set {  resourceName = value;  }
        }

        public String Description
        {
            get {  return resourceDescription;  }
            set {  resourceDescription = value;  }
        }

        public String Table
        {
            get { return resourceTable;  }
            set { resourceTable = value; }
        }

    }

    public class LocalizedCategoryAttribute : CategoryAttribute
    {
        readonly ResourceManager _resourceManager;
        readonly string _resourceKey;

        public LocalizedCategoryAttribute(string resourceKey)
        {
            _resourceManager = CommandArgUsage.ResourceManager;
            _resourceKey = resourceKey;
        }

        protected override string GetLocalizedString(string value)
        {
            string category = _resourceManager.GetString(_resourceKey);
            return string.IsNullOrWhiteSpace(category) ? string.Format("[[{0}]]", _resourceKey) : category;
        }
    }
}
