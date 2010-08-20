//
// $Id$
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#ifndef _PEP2MZIDENT_HPP_
#define _PEP2MZIDENT_HPP_

#include "MzIdentML.hpp"
#include "KwCVMap.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/utility/misc/Export.hpp"

#include <vector>


namespace pwiz{
namespace mziddata{

using namespace pwiz::data::pepxml;
typedef boost::shared_ptr<MzIdentML> MzIdentMLPtr;

/// Translates data from a MinimumPepXML object into a MzIdentML
/// object tree when a translation is known. The MzIdentML object
/// initializes a cvList with the "MS" and "UO" elements.
///
/// The translate method is used to copy data. The clear method resets
/// the MzIdentML object, null's out the MSMSPipelineAnalysis.
class PWIZ_API_DECL Pep2MzIdent
{

public:
    /// Initialized the member variables. Also adds the "MS" and "UO"
    /// CV's to the cvList.
    Pep2MzIdent(const MSMSPipelineAnalysis& mspa,
                MzIdentMLPtr result = MzIdentMLPtr(new MzIdentML()));

    /// Resets the member variables.
    void clear();

    bool operator()(const MSMSPipelineAnalysis& pepxml, MzIdentMLPtr mzid);
    
    /// Translates all known tags in the pepXML object tree into the
    /// MzIdentML object tree. The resulting MzIdentMLPtr is returned.
    MzIdentMLPtr translate();

    /// Clears fields and then sets the _mspa field to the address of
    /// mspa.
    void setMspa(const MSMSPipelineAnalysis& mspa);
    
    /// Returns the MzIdentMLPtr object. If a translation has not been
    /// done, or if clear has been called, then an empty MzIdentML
    /// will be return in the MzIdentMLPtr object.
    MzIdentMLPtr getMzIdentML() const;

    void setDebug(bool debug);

    bool getDebug() const;
    
    void setVerbose(bool verbose);

    bool getVerbose() const;

    void addParamMap(std::vector<CVMapPtr>& map);

    void setParamMap(std::vector<CVMapPtr>& map);

    const std::vector<CVMapPtr>& getParamMap() const;
    
private:
    class Impl;
    boost::shared_ptr<Impl> pimpl;
    
};

} // namespace mziddata
} // namespace pwiz


#endif
