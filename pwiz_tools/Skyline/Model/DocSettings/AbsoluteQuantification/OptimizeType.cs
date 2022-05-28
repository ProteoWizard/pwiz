namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class OptimizeType
    {
        public static readonly OptimizeType LOD = new OptimizeType("lod");
        public static readonly OptimizeType LOQ = new OptimizeType("loq");
        private OptimizeType(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public override string ToString()
        {
            return Name;
        }
    }
}
