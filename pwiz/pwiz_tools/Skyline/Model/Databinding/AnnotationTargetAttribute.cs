using System;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Databinding
{
    public class AnnotationTargetAttribute : Attribute
    {
        public AnnotationTargetAttribute(AnnotationDef.AnnotationTarget target)
        {
            AnnotationTarget = target;
        }
        public AnnotationDef.AnnotationTarget AnnotationTarget { get; set; }
    }
}
