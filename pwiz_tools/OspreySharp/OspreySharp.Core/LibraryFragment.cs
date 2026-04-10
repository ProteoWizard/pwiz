namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A single fragment peak from a spectral library. Maps to osprey-core/src/types.rs LibraryFragment.
    /// </summary>
    public class LibraryFragment
    {
        public double Mz { get; set; }
        public float RelativeIntensity { get; set; }
        public FragmentAnnotation Annotation { get; set; }
    }
}
