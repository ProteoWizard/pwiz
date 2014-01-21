//
// $Id$
//
// Robert Burke <robert.burke@cshs.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef FITFROMPEPXML_HPP
#define FITFROMPEPXML_HPP

#include "pepxml/PeptideRTSystem.hpp"
#include "pepxml/DataExtractor.hpp"
#include <vector>

namespace pwiz {
namespace rtapprox {

class FitFromPepXML
{
public:
    static const int NUMENTRIES;

    static const char AMINOACIDS[];

    virtual ~FitFromPepXML() {}
    
    static std::auto_ptr<FitFromPepXML> create();

    static std::auto_ptr<FitFromPepXML> create(const char* pepxmlFile,
                                          float tol=0.0,
                                          float rtmin = -1.0,
                                          float rtmax = -1.0,
                                          unsigned limit=0);

    static std::auto_ptr<FitFromPepXML> createFromMatrix(
        const boost::numeric::ublas::matrix<double>& peps,
        const boost::numeric::ublas::vector<double>& rts);

    virtual bool createSystem(std::vector<pwiz::pepxml::Peptide>& peptideList) = 0;

    virtual const boost::numeric::ublas::vector<double> fit() = 0;

    virtual const boost::numeric::ublas::vector<double> load() = 0;

    virtual const boost::numeric::ublas::vector<double> load(const char* pepXMLFile,
                                unsigned limit = 0) = 0;
    
    virtual const pwiz::pepxml::PeptideRTSystem* getSystem() = 0;

    virtual const boost::numeric::ublas::matrix<double>& getPeptideMatrix() const = 0;

    virtual const boost::numeric::ublas::vector<double>& getRTVector() const = 0;

    virtual const std::vector<pwiz::pepxml::Peptide>& getPeptideList() const = 0;

    virtual bool bad() const = 0;

    virtual float getRtMin() const = 0;

    virtual void setRtMin(float rtmin) = 0;
    
    virtual float getRtMax() const = 0;
    
    virtual void setRtMax(float rtmax) = 0;
    
    virtual unsigned getLimit() const = 0;

    virtual void setLimit(unsigned limit) = 0;

    virtual boost::numeric::ublas::vector<double> countPeptide(
        const char* peptide_str) = 0;

    virtual const boost::numeric::ublas::matrix<double>& getAtA() = 0;
};

}
}
#endif // PEPTIDEMATRIXFACTORY_HPP


