using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.MSGraph;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI
{
    public partial class msGraphExtension : UserControl
    {
        public bool propertiesVisible;
        public MSGraphControl graph { get; }

        public PropertyGrid propertiesGrid { get; }


        public msGraphExtension()
        {
            InitializeComponent();
            propertiesVisible = false;
            splitContainer1.Panel2Collapsed = true;
            graph = graphControl;
            propertiesGrid = gridSpectrumInfo;
        }

        public void setPropertiesObject(object spectrumProperties)
        {
            gridSpectrumInfo.SelectedObject = spectrumProperties;
        }

        public void setPropertiesVisibility(bool visible)
        {
            propertiesVisible = visible;
            splitContainer1.Panel2Collapsed = !propertiesVisible;
        }

        public void showProperties()
        {
            propertiesVisible = true;
            splitContainer1.Panel2Collapsed = !propertiesVisible;
        }

        public void hideProperties()
        {
            propertiesVisible = false;
            splitContainer1.Panel2Collapsed = !propertiesVisible;
        }

        public void refresh()
        {
            gridSpectrumInfo.Refresh();
        }

    }

    /// <summary>
    /// GlobalizedPropertyDescriptor enhances the base class bay obtaining the display name for a property
    /// from the resource.
    ///
    /// The classes in RefineInputLocalization.cs were modified from Descriptors.cs at:
    ///     https://www.codeproject.com/script/Articles/ViewDownloads.aspx?aid=2138
    ///
    /// </summary>
    public class GlobalizedPropertyDescriptor : PropertyDescriptor
    {
        private readonly PropertyDescriptor basePropertyDescriptor;
        public bool ReadOnly = true;

        public GlobalizedPropertyDescriptor(PropertyDescriptor basePropertyDescriptor) : base(basePropertyDescriptor)
        {
            this.basePropertyDescriptor = basePropertyDescriptor;
        }

        public override bool CanResetValue(object component)
        {
            return basePropertyDescriptor.CanResetValue(component);
        }

        public override Type ComponentType
        {
            get => basePropertyDescriptor.ComponentType;
        }

        // public override string DisplayName
        // {
        //     get
        //     {
        //         // Get display name from CommandArgName
        //         var displayNameKey = RefineInputObject.REFINE_RESOURCE_KEY_PREFIX + basePropertyDescriptor.Name;
        //         return CommandArgUsage.ResourceManager.GetString(displayNameKey);
        //     }
        // }
        //
        // public override string Description
        // {
        //     get
        //     {
        //         var displayNameKey = RefineInputObject.REFINE_RESOURCE_KEY_PREFIX + basePropertyDescriptor.Name;
        //         var description = CommandArgUsage.ResourceManager.GetString(displayNameKey);
        //         // Remove newlines
        //         description = description != null ? description.Replace('\n', ' ') : string.Empty;
        //         return description;
        //     }
        // }
        //
        // public override string Category
        // {
        //     get
        //     {
        //         var variableName = basePropertyDescriptor.Name;
        //         var variable = (RefineVariable)Enum.Parse(typeof(RefineVariable), variableName);
        //         return (int)variable <= (int)RefineVariable.auto_select_transitions
        //             ? CommandArgUsage.ResourceManager.GetString("CommandArgs_GROUP_REFINEMENT")
        //             : CommandArgUsage.ResourceManager.GetString("CommandArgs_GROUP_REFINEMENT_W_RESULTS");
        //     }
        // }

        public override object GetValue(object component)
        {
            // Doesn't display default values to highlight changed ones
            var value = basePropertyDescriptor.GetValue(component);
            if (value == null)
                return string.Empty;
            if (value is bool && !(bool)value)
                return string.Empty;

            return value;
        }

        public override bool IsReadOnly
        {
            get => ReadOnly;
        }

        public override string Name
        {
            get => basePropertyDescriptor.Name;
        }

        public override Type PropertyType
        {
            get => basePropertyDescriptor.PropertyType;
        }

        public override void ResetValue(object component)
        {
            basePropertyDescriptor.ResetValue(component);
        }

        public override bool ShouldSerializeValue(object component)
        {
            return basePropertyDescriptor.ShouldSerializeValue(component);
        }

        public override void SetValue(object component, object value)
        {
            basePropertyDescriptor.SetValue(component, value);
        }
    }

    /// <summary>
    /// GlobalizedObject implements ICustomTypeDescriptor to enable 
    /// required functionality to describe a type (class).<br></br>
    /// The main task of this class is to instantiate our own property descriptor 
    /// of type GlobalizedPropertyDescriptor.  
    /// </summary>
    public class GlobalizedObject : ICustomTypeDescriptor
    {
        private PropertyDescriptorCollection globalizedProps;

        public String GetClassName() => TypeDescriptor.GetClassName(this, true);

        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);

        public String GetComponentName() => TypeDescriptor.GetComponentName(this, true);

        public TypeConverter GetConverter() => TypeDescriptor.GetConverter(this, true);

        public EventDescriptor GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);

        public PropertyDescriptor GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);

        public object GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);

        public EventDescriptorCollection GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes, true);

        public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this, true);

        public object GetPropertyOwner(PropertyDescriptor pd) => this;

        /// <summary>
        /// Called to get the properties of a type.
        /// </summary>
        /// <param name="attributes"></param>
        /// <returns></returns>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            if (globalizedProps == null)
            {
                // Get the collection of properties
                PropertyDescriptorCollection baseProps = TypeDescriptor.GetProperties(this, attributes, true);

                globalizedProps = new PropertyDescriptorCollection(null);

                // For each property use a property descriptor of our own that is able to be globalized
                foreach (PropertyDescriptor oProp in baseProps)
                {
                    globalizedProps.Add(new GlobalizedPropertyDescriptor(oProp));
                }
            }
            return globalizedProps;
        }

        public PropertyDescriptorCollection GetProperties()
        {
            // Only do once
            if (globalizedProps == null)
            {
                // Get the collection of properties
                PropertyDescriptorCollection baseProps = TypeDescriptor.GetProperties(this, true);
                globalizedProps = new PropertyDescriptorCollection(null);

                // For each property use a property descriptor of our own that is able to be globalized
                foreach (PropertyDescriptor oProp in baseProps)
                {
                    globalizedProps.Add(new GlobalizedPropertyDescriptor(oProp));
                }
            }
            return globalizedProps;
        }
    }
}
