using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace ToolServiceCmd
{
    public class ImportAnnotationsCommand : BaseCommand<ImportAnnotationsCommand.Options>
    {
        public override int PerformCommand(Options options)
        {
            return 0;
        }

        [Verb("ImportAnnotations")]
        public class Options : BaseOptions
        {
            [Option]
            public string AnnotationFile { get; set; }
        }
    }
}
