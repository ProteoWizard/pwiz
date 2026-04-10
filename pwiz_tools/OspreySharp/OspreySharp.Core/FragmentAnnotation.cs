namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Annotation metadata for a library fragment peak. Maps to osprey-core/src/types.rs FragmentAnnotation.
    /// </summary>
    public class FragmentAnnotation
    {
        public IonType IonType { get; set; }
        public byte Ordinal { get; set; }
        public byte Charge { get; set; }
        public NeutralLoss NeutralLoss { get; set; }

        public FragmentAnnotation()
        {
            IonType = IonType.Unknown;
            Ordinal = 0;
            Charge = 1;
        }
    }
}
