using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssortResources
{
    public class ResourceReference
    {
        public ResourceReference(string resourceFileName, int resourceFileNameOffset, string resourceIdentifier,
            int resourceIdentifierOffset)
        {
            ResourceFileName = resourceFileName;
            ResourceFileNameOffset = resourceFileNameOffset;
            ResourceIdentifier = resourceIdentifier;
            ResourceIdentifierOffset = resourceIdentifierOffset;
        }

        public string ResourceFileName { get; }
        public int ResourceFileNameOffset { get; }
        public string ResourceIdentifier { get; }
        public int ResourceIdentifierOffset { get; }
    }
}
