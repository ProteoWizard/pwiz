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
            if (obj.GetType() != this.GetType()) return false;
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
