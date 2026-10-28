/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// The settings of an Osprey run as a PropertyGrid shows them: one
    /// <see cref="OspreySettingDescriptor"/> per setting of the run's scopes, in help order, each
    /// shown under its label in the category of its help section, with its usage text in the help
    /// box. The descriptors edit the <see cref="OspreyAdditionalSettings"/>' own Setting objects,
    /// so what the grid sets is what the command line reads. The component-model boilerplate is
    /// delegated to <see cref="TypeDescriptor"/>, as <see cref="GlobalizedObject"/> does.
    /// </summary>
    public sealed class OspreySettingsObject : ICustomTypeDescriptor
    {
        private readonly PropertyDescriptorCollection _properties;

        public OspreySettingsObject(OspreyAdditionalSettings settings, ICollection<OspreyPropertyScope> scopes)
        {
            Settings = settings;
            var descriptors = new List<PropertyDescriptor>();
            foreach (var property in settings.Properties.Properties.Where(p => scopes.Contains(p.Scope)))
            {
                if (settings.Settings.TryGetValue(property.Name, out var setting))
                    descriptors.Add(new OspreySettingDescriptor(settings, property, setting));
            }
            _properties = new PropertyDescriptorCollection(descriptors.ToArray(), true);
        }

        public OspreyAdditionalSettings Settings { get; }

        /// <summary>The descriptors in help order: the rows the grid shows before it sorts them.</summary>
        public IEnumerable<OspreySettingDescriptor> Descriptors => _properties.Cast<OspreySettingDescriptor>();

        /// <summary>The descriptor shown under <paramref name="label"/>, or null when none is.</summary>
        public OspreySettingDescriptor FindByLabel(string label)
        {
            return Descriptors.FirstOrDefault(d => d.DisplayName == label);
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return _properties;
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return _properties;
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);

        public string GetClassName() => TypeDescriptor.GetClassName(this, true);

        public string GetComponentName() => TypeDescriptor.GetComponentName(this, true);

        public TypeConverter GetConverter() => TypeDescriptor.GetConverter(this, true);

        public EventDescriptor GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);

        public PropertyDescriptor GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);

        public object GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);

        public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this, true);

        public EventDescriptorCollection GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes, true);
    }

    /// <summary>
    /// One setting as a PropertyGrid row: named by its argument, shown under its label in the
    /// category of its help section, with the tooltip text as its description. A flag is a bool
    /// (a checkbox); everything else is text, a list's text chosen from its values
    /// (<see cref="OspreyListValuesConverter"/>). A value the argument does not take is refused
    /// with an <see cref="ArgumentException"/> naming the setting, which the grid shows. A value
    /// that is not Osprey's default is shown bold and can be reset to it.
    /// </summary>
    public sealed class OspreySettingDescriptor : PropertyDescriptor
    {
        private readonly OspreyAdditionalSettings _settings;
        private readonly OspreyProperty _property;
        private readonly TypeConverter _converter;

        public OspreySettingDescriptor(OspreyAdditionalSettings settings, OspreyProperty property, AbstractDdaSearchEngine.Setting setting)
            : base(property.Name, null)
        {
            _settings = settings;
            _property = property;
            Setting = setting;
            if (property.Type == OspreyPropertyType.list)
                _converter = new OspreyListValuesConverter(setting.ValidValues);
        }

        /// <summary>The setting this row edits: the same object the command line reads.</summary>
        public AbstractDdaSearchEngine.Setting Setting { get; }

        public override string DisplayName => _property.Label;

        public override string Description => _property.Tooltip;

        // A PropertyGrid draws a category with mnemonics, so an ampersand in a section title ("FDR & Protein Inference") is doubled
        public override string Category => _property.Group?.Replace(@"&", @"&&");

        public override Type ComponentType => typeof(OspreySettingsObject);

        public override Type PropertyType => _property.Type == OspreyPropertyType.flag ? typeof(bool) : typeof(string);

        public override bool IsReadOnly => false;

        public override TypeConverter Converter => _converter ?? base.Converter;

        public override object GetValue(object component)
        {
            return Setting.Value;
        }

        /// <summary>
        /// Sets the setting from what the grid holds: a flag's bool as it is, other text checked by
        /// the property's rules and the setting's own (<see cref="OspreyAdditionalSettings.Validate"/>)
        /// before it is kept (<see cref="OspreyAdditionalSettings.SetValue"/>).
        /// </summary>
        public override void SetValue(object component, object value)
        {
            if (value is bool flag)
            {
                Setting.Value = flag;
            }
            else
            {
                string text = value?.ToString() ?? string.Empty;
                _settings.Validate(text, Setting);
                _settings.SetValue(Setting, text);
            }
            OnValueChanged(component, EventArgs.Empty);
        }

        public override bool CanResetValue(object component)
        {
            return !Setting.IsDefault;
        }

        /// <summary>Returns the setting to Osprey's default, which leaves the argument off the command line.</summary>
        public override void ResetValue(object component)
        {
            Setting.Value = Setting.DefaultValue;
            OnValueChanged(component, EventArgs.Empty);
        }

        /// <summary>True when the value is not Osprey's default, which the grid shows in bold.</summary>
        public override bool ShouldSerializeValue(object component)
        {
            return !Setting.IsDefault;
        }
    }

    /// <summary>
    /// The choices of a list setting, as the grid's drop-down offers them: the setting's valid
    /// values and nothing else, a blank one (which leaves the argument out) shown as blank.
    /// </summary>
    public sealed class OspreyListValuesConverter : StringConverter
    {
        private readonly StandardValuesCollection _values;

        public OspreyListValuesConverter(IEnumerable<string> values)
        {
            _values = new StandardValuesCollection(values.ToArray());
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return _values;
        }
    }
}
