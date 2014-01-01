//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#define PWIZ_SOURCE

#include "MetadataReporter.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


namespace bfs = boost::filesystem;


PWIZ_API_DECL void MetadataReporter::open(const DataInfo& dataInfo)
{
    bfs::path outputFile = dataInfo.outputDirectory;
    outputFile /= dataInfo.sourceFilename + ".metadata.txt";

    bfs::ofstream os(outputFile);
    if (!os)
        throw runtime_error(("[MetadataReporter] Unable to open file " + outputFile.string()).c_str());

    if (dataInfo.log)
        *dataInfo.log << "[MetadataReporter] Writing file " << outputFile.string() << endl;

    TextWriter write(os, 0);
    write(dataInfo.msd.fileDescription);
    write("sampleList:", dataInfo.msd.samplePtrs);
    write("instrumentConfigurationList:", dataInfo.msd.instrumentConfigurationPtrs);
    write("softwareList:", dataInfo.msd.softwarePtrs);
    write("dataProcessingList", dataInfo.msd.dataProcessingPtrs);
}


} // namespace analysis 
} // namespace pwiz

