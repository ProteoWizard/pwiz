using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSStatArgsCollector
{
    public enum Arg
    {
        // Shared
        normalization,
        msLevel,
        featureSelection,
        outputFolder,
        controlGroup,
        qValueCutoff,

        // QC
        width,
        height,

        // Design Sample Size
        numSample,
        power,
        FDR,
        ldfc,
        udfc,
    }
}
