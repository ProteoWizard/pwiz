﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.IO;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;

namespace pwiz.Skyline.Model.DocSettings
{
    public struct FullScanAcquisitionMethod : IAuditLogObject, IEquatable<FullScanAcquisitionMethod>
    {
        public static readonly FullScanAcquisitionMethod None = default(FullScanAcquisitionMethod);
        public static readonly FullScanAcquisitionMethod DIA = new FullScanAcquisitionMethod(@"DIA",
            () => EnumNames.FullScanAcquisitionMethod_DIA, () => DocSettingsResources.FullScanAcquisitionMethod_DIA_TOOLTIP);
        public static readonly FullScanAcquisitionMethod PRM = new FullScanAcquisitionMethod(@"PRM",
            () => EnumNames.FullScanAcquisitionMethod_PRM, () => DocSettingsResources.FullScanAcquisitionMethod_PRM_TOOLTIP);
        public static readonly FullScanAcquisitionMethod DDA = new FullScanAcquisitionMethod(@"DDA",
            () => EnumNames.FullScanAcquisitionMethod_DDA, () => DocSettingsResources.FullScanAcquisitionMethod_DDA_TOOLTIP);
        public static readonly FullScanAcquisitionMethod SureQuant = new FullScanAcquisitionMethod(@"SureQuant",
            () => EnumNames.FullScanAcquisitionMethod_SureQuant,
            () => DocSettingsResources.FullScanAcquisitionMethod_SureQuant_TOOLTIP);
        public static readonly FullScanAcquisitionMethod Targeted = new FullScanAcquisitionMethod(@"Targeted",
            () => EnumNames.FullScanAcquisitionMethod_Targeted,
            () => DocSettingsResources.FullScanAcquisitionMethod_Targeted_TOOLTIP);

        public static readonly ImmutableList<FullScanAcquisitionMethod> ALL =
            ImmutableList.ValueOf(new[] {None, DIA, PRM, DDA, SureQuant, Targeted });

        public static readonly ImmutableList<FullScanAcquisitionMethod> AVAILABLE =
            ImmutableList.ValueOf(new[] { None, DIA, PRM, DDA, SureQuant });
        private readonly Func<string> _getLabelFunc;
        private readonly Func<string> _getTooltipFunc;
        private readonly string _name;

        private FullScanAcquisitionMethod(string name, Func<string> getLabelFunc, Func<string> getTooltipFunc)
        {
            _name = name;
            _getLabelFunc = getLabelFunc;
            _getTooltipFunc = getTooltipFunc;
        }

        public string Name
        {
            get { return _name ?? @"None"; } //;
        }

        public string Label
        {
            get
            {
                if (_getLabelFunc == null)
                {
                    return DocSettingsResources.FullScanAcquisitionExtension_LOCALIZED_VALUES_None;
                }
                return _getLabelFunc();
            }
        }

        public string Tooltip
        {
            get
            {
                return _getTooltipFunc?.Invoke() ?? string.Empty;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public static FullScanAcquisitionMethod FromName(string name)
        {
            if (name == null)
            {
                return None;
            }
            foreach (var method in ALL)
            {
                if (method.Name == name)
                {
                    return method;
                }
            }
            throw new InvalidDataException(string.Format(DocSettingsResources.FullScanAcquisitionMethod_FromName__0__is_not_a_valid_Full_Scan_Acquisition_Method, name));
        }

        public static FullScanAcquisitionMethod? FromLegacyName(string legacyName)    // Skyline 1.2 and earlier
        {
            if (legacyName == null)
            {
                return null;
            }
            if (legacyName == @"Single")
            {
                return Targeted;
            }
            if (legacyName == @"Multiple")
            {
                return DIA;
            }
            return None;
        }

        string IAuditLogObject.AuditLogText
        {
            get
            {
                return AuditLogParseHelper.GetParseString(ParseStringType.enum_fn,
                    @"FullScanAcquisitionMethod_" + Name);
            }
        }

        bool IAuditLogObject.IsName => true;

        [Pure]
        public bool Equals(FullScanAcquisitionMethod other)
        {
            return string.Equals(_name, other._name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is FullScanAcquisitionMethod other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_name != null ? _name.GetHashCode() : 0);
        }

        public static bool operator ==(FullScanAcquisitionMethod left, FullScanAcquisitionMethod right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FullScanAcquisitionMethod left, FullScanAcquisitionMethod right)
        {
            return !left.Equals(right);
        }
    }
}
