//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

#ifndef _XIC_HPP_
#define _XIC_HPP_

#include "sqlite3pp.h"
#include "Embedder.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"

#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE



namespace XIC {
    
struct XICConfiguration
{
    XICConfiguration(bool AlignRetentionTime = false, const std::string& RTFolder = "", double MaxQValue = 0.05,
                     const pwiz::util::IntegerSet& MonoisotopicAdjustmentSet = pwiz::util::IntegerSet(0, 2),
                     int RetentionTimeLowerTolerance = 30, int RetentionTimeUpperTolerance = 30,
                     pwiz::chemistry::MZTolerance ChromatogramMzLowerOffset = pwiz::chemistry::MZTolerance(10, pwiz::chemistry::MZTolerance::PPM),
                     pwiz::chemistry::MZTolerance ChromatogramMzUpperOffset = pwiz::chemistry::MZTolerance(10, pwiz::chemistry::MZTolerance::PPM));


    bool AlignRetentionTime;
    std::string RTFolder;
    double MaxQValue;
    pwiz::util::IntegerSet MonoisotopicAdjustmentSet;
    int RetentionTimeLowerTolerance;
    int RetentionTimeUpperTolerance;
    pwiz::chemistry::MZTolerance ChromatogramMzLowerOffset;
    pwiz::chemistry::MZTolerance ChromatogramMzUpperOffset;

    operator std::string() const;
};


int EmbedMS1ForFile(sqlite3pp::database& idpDb,
                     const std::string& idpDBFilePath,
                     const std::string& sourceFilePath,
                     const std::string& sourceId,
                     XICConfiguration& cofig,
                     pwiz::util::IterationListenerRegistry* ilr,
                     int currentFile, int totalFiles);

} // namespace Embedder
END_IDPICKER_NAMESPACE


#endif // _XIC_HPP_
