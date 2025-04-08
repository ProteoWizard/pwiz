using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class LibraryAlignments
    {
        public static readonly Producer<Parameter, LibraryAlignments> PRODUCER = new Producer();
        private Dictionary<string, AlignmentFunction> _alignmentFunctions;

        public LibraryAlignments(Parameter parameter, Dictionary<string, AlignmentFunction> alignmentFunctions)
        {
            Param = parameter;
            _alignmentFunctions = alignmentFunctions;
        }

        public Parameter Param { get; }

        public class Parameter
        {
            public Parameter(AlignmentTarget target, Library library)
            {
                Target = target;
                Library = library;
            }

            public AlignmentTarget Target { get; }
            public Library Library { get; }
        }

        public AlignmentFunction GetAlignmentFunction(string name)
        {
            _alignmentFunctions.TryGetValue(name, out var result);
            return result;
        }

        public AlignmentFunction GetAlignmentFunction(MsDataFileUri msDataFileUri)
        {
            var name = msDataFileUri.GetFileNameWithoutExtension();
            foreach (var entry in _alignmentFunctions)
            {
                if (name == Path.GetFileNameWithoutExtension(entry.Key))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        public IEnumerable<KeyValuePair<string, AlignmentFunction>> GetAllAlignmentFunctions()
        {
            return _alignmentFunctions.AsEnumerable();
        }

        private class Producer : Producer<Parameter, LibraryAlignments>
        {
            public override LibraryAlignments ProduceResult(ProductionMonitor productionMonitor, Parameter parameter, IDictionary<WorkOrder, object> inputs)
            {
                var allRetentionTimes = parameter.Library.GetAllRetentionTimes();
                var alignments = new Dictionary<string, AlignmentFunction>();
                if (allRetentionTimes != null)
                {
                    for (int i = 0; i < allRetentionTimes.Length; i++)
                    {
                        productionMonitor.SetProgress(100 * i / allRetentionTimes.Length);
                        var file = parameter.Library.LibraryFiles[i];
                        var dict = allRetentionTimes[i];
                        var measuredTimes =
                            dict.Select(kvp => new MeasuredRetentionTime(kvp.Key, kvp.Value, true)).ToList();
                        var alignmentFunction =
                            parameter.Target.PerformAlignment(dict, productionMonitor.CancellationToken);
                        if (alignmentFunction != null)
                        {
                            alignments.Add(file, alignmentFunction);
                        }
                    }
                }

                return new LibraryAlignments(parameter, alignments);
            }
        }
    }
}
