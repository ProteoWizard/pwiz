using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourcesOrganizer.ResourcesModel
{
    public record LocalizationCsvRecord
    {
        public string Name { get; init; }
        public string Comment { get; init; }
        public string English { get; init; }
        public string Translation { get; init; }
        public string Issue { get; init; }
        public int? FileCount { get; init; }
        public string? File { get; init; }
    }
}
