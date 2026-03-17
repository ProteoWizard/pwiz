//
// $Id$
//
//
// Original author: Brian Pratt <bspratt .at. proteinms.net>
//
// Copyright 2025 University of Washington - Seattle, WA
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


//
// Implementation of helper functions for  LC traces in Bruker chromatography-data.sqlite files
// See accompanying file "pwiz\data\vendor_readers\Bruker\hystar_enum_definitions.txt" for details provided by Bruker
//


#define PWIZ_SOURCE

#include "CompassData.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include "pwiz/data/vendor_readers/Bruker/Reader_Bruker_Detail.hpp"
#include "sqlite3pp.h"
#include <sstream>

using namespace pwiz::util;
using namespace pwiz::vendor_api::Bruker;

namespace pwiz {
namespace msdata {
namespace detail {

std::vector<vendor_api::Bruker::ChromatogramPtr> readChromatographyDataSqlite(const std::string rootpath)
{
    std::vector<vendor_api::Bruker::ChromatogramPtr> results;
    try
    {
        bfs::path chromFilepath = bfs::path(rootpath) / "chromatography-data.sqlite";
        if (!bfs::exists(chromFilepath))
            return results;

        sqlite3pp::database db(chromFilepath.string());
        sqlite3pp::query q(db, "SELECT Id FROM TraceSources ORDER BY Id");

        for (sqlite3pp::query::iterator itr = q.begin(); itr != q.end(); ++itr)
        {
            int traceId = itr->get<int>(0);

            vendor_api::Bruker::ChromatogramPtr chrom(new vendor_api::Bruker::Chromatogram);

            int unitValue = 0;
            double timeOffset = 0.0;

            // Get trace metadata from TraceSources
            std::ostringstream sourceQuery;
            sourceQuery << "SELECT Type, Unit, Description, Instrument, TimeOffset FROM TraceSources WHERE Id=" << traceId;
            sqlite3pp::query sq(db, sourceQuery.str().c_str());
            sqlite3pp::query::iterator sitr = sq.begin();
            if (sitr != sq.end())
            {
                chrom->description = sitr->get<std::string>(2);
                chrom->instrument = sitr->get<std::string>(3);
                timeOffset = sitr->get<double>(4);
                unitValue = sitr->get<int>(1);

                // store the raw enum values into the strongly typed enums
                chrom->type = static_cast<TraceType>(sitr->get<int>(0));
                if (unitValue == static_cast<int>(TraceUnit::NoneUnit))
                {
                    unitValue = static_cast<int>(TraceUnit::Intensity); // default to Intensity if no unit specified

                    if (chrom->type == TraceType::ChromPressure)
                    {
                        if (bal::contains(chrom->description, "[psi]"))
                            unitValue = static_cast<int>(TraceUnit::Pressure_psi);
                        else if (bal::icontains(chrom->description, "[bar]"))
                            unitValue = static_cast<int>(TraceUnit::Pressure_bar);
                    }
                }
                chrom->units = static_cast<TraceUnit>(unitValue);
            }

            // Use sqlite3pp wrapper for TraceChunks (replace raw sqlite3 C API usage)
            std::ostringstream chunksQuery;
            chunksQuery << "SELECT Times, Intensities FROM TraceChunks WHERE Trace=" << traceId << " ORDER BY ROWID";
            sqlite3pp::query chunksQ(db, chunksQuery.str().c_str());

            for (sqlite3pp::query::iterator citr = chunksQ.begin(); citr != chunksQ.end(); ++citr)
            {
                const void* timesBlob = citr->get<const void*>(0);
                const void* intensitiesBlob = citr->get<const void*>(1);

                if (!timesBlob || !intensitiesBlob)
                    continue;

                const double* timesData = reinterpret_cast<const double*>(timesBlob);
                int numTimes = citr->column_bytes(0) / sizeof(double);
                chrom->times.insert(chrom->times.end(), timesData, timesData + numTimes);

                const float* intensitiesData = reinterpret_cast<const float*>(intensitiesBlob);
                int numIntensities = citr->column_bytes(1) / sizeof(float);
                for (int i = 0; i < numIntensities; ++i)
                    chrom->intensities.push_back(static_cast<double>(intensitiesData[i]));
            }

            for (double& intensity : chrom->intensities)

                pwiz::msdata::detail::Bruker::traceUnitToCVID(chrom->units, intensity); // Scale values as needed to fit available standard unit types.


            if (timeOffset != 0.0)
            {
                for (double& time : chrom->times)
                    time += timeOffset;
            }
            results.push_back(chrom);
        }
    }
    catch (...)
    {
        // ignore errors, return empty list
    }

    return results;
}

} // namespace detail
} // namespace msdata
} // namespace pwiz
