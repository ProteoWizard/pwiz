/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class ViewSpecLayout : Immutable, IAuditLogObject
    {
        public ViewSpecLayout(ViewSpec viewSpec, ViewLayoutList layouts)
        {
            ViewSpec = viewSpec;
            ViewLayoutList = EnsureName(layouts, viewSpec.Name);
        }

        [TrackChildren(ignoreName:true)]
        public ViewSpec ViewSpec { get; private set; }
        public ViewLayoutList ViewLayoutList { get; private set; }
        public string Name
        {
            get { return ViewSpec.Name; }
        }

        public ViewLayout DefaultViewLayout
        {
            get
            {
                if (string.IsNullOrEmpty(ViewLayoutList.DefaultLayoutName))
                {
                    return null;
                }
                return ViewLayoutList.FindLayout(ViewLayoutList.DefaultLayoutName);
            }
        }

        public ViewSpecLayout ChangeName(string name)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ViewSpec = ViewSpec.SetName(name);
                im.ViewLayoutList = ViewLayoutList.ChangeViewName(name);
            });
        }

        [Track]
        public string DefaultLayoutName
        {
            get { return ViewLayoutList.DefaultLayoutName; }
        }

        public ImmutableList<ViewLayout> Layouts
        {
            get { return ViewLayoutList.Layouts; }
        }

        private static ViewLayoutList EnsureName(ViewLayoutList layouts, string name)
        {
            layouts = layouts ?? ViewLayoutList.EMPTY;
            return layouts.ViewName == name ? layouts : layouts.ChangeViewName(name);
        }
        public string AuditLogText { get { return ViewSpec.Name; } }
        public bool IsName { get { return true; } }

        protected bool Equals(ViewSpecLayout other)
        {
            return Equals(ViewSpec, other.ViewSpec) && Equals(ViewLayoutList, other.ViewLayoutList);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ViewSpecLayout) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ViewSpec != null ? ViewSpec.GetHashCode() : 0) * 397) ^ (ViewLayoutList != null ? ViewLayoutList.GetHashCode() : 0);
            }
        }

        public static bool operator ==(ViewSpecLayout left, ViewSpecLayout right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ViewSpecLayout left, ViewSpecLayout right)
        {
            return !Equals(left, right);
        }
    }
}
