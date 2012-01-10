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


#include "FitFromPepXML.hpp"
#include "math/LinearLeastSquares.hpp"
#include <boost/numeric/ublas/matrix_proxy.hpp>
#include <stdexcept>
#include <math.h>
#include <stdlib.h>
#include <iostream>
#include <iomanip>
#include <vector>

using namespace std;
using pwiz::math::LinearLeastSquares;

using namespace pwiz::pepxml;
using namespace pwiz::math::types;

namespace ublas = boost::numeric::ublas;

namespace pwiz {
namespace rtapprox {

class FitFromPepXMLImpl : public FitFromPepXML
{
public:
    FitFromPepXMLImpl();

    FitFromPepXMLImpl(const char* pepXMLFile, float tol, float rtmin = -1, float rtmax = -1, unsigned limit = 0);

    FitFromPepXMLImpl(const dmatrix& peps, const dvector& rts);
    
    virtual bool createSystem(vector<Peptide>& peptideList);

    virtual const dvector fit();

    virtual const dvector load();
    
    virtual const dvector load(const char* pepXMLFile, unsigned limit=0);
    
    virtual const PeptideRTSystem* getSystem()
    {
        return system.get();
    }

    virtual const dmatrix& getPeptideMatrix() const
    {
        if (system.get() == NULL)
            throw runtime_error("no system have been loaded.");

        return system->getPeptideCounts();
    }

    virtual const dvector& getRTVector() const
    {
        if (system.get() == NULL)
            throw runtime_error("no system have been loaded.");

        return system->getRetentionTimes();
    }

    virtual const vector<Peptide>& getPeptideList() const
    {
        if (extractor.get() == NULL)
            throw runtime_error("no results have been calculated.");

        return extractor->getPeptideList();
    }
    
    virtual bool bad() const
    {
        return bad_;
    }

    virtual float getRtMin() const
    {
        return rtmin_;
    }
    
    virtual void setRtMin(float rtmin)
    {
        rtmin_ = rtmin;
    }
    
    virtual float getRtMax() const
    {
        return rtmin_;
    }
    
    virtual void setRtMax(float rtmax)
    {
        rtmax_ = rtmax;
    }
    
    virtual unsigned getLimit() const
    {
        return limit_;
    }

    virtual void setLimit(unsigned limit)
    {
        limit_ = limit;
    }
    
    virtual dvector countPeptide(const char* peptide_str);

    virtual const dmatrix& getAtA()
    {
        return system->getPeptideCounts();
    }
    
protected:
    bool bad_;
    
    const char* pepxmlFile;

    float tol_;
    float rtmin_;
    float rtmax_;
    unsigned limit_;

    auto_ptr<PeptideRTSystem> system;
    LinearLeastSquares<pwiz::math::LinearLeastSquaresType_QR> lls;
    auto_ptr<DataExtractor> extractor;
};

// TODO Move the number of matrix columns into a header
const char FitFromPepXML::AMINOACIDS[] = {
    'A',       // ala
    'R',       // arg
    'N',       // asn
    'D',       // asp   
    'C',       // cys
    'E',       // glu   
    'Q',       // gln
    'G',       // gly
    'H',       // his
    'I',       // ile
    'L',       // leu
    'K',       // lys
    'M',       // met
    'F',       // phe
    'P',       // pro
    'S',       // ser
    'T',       // thr
    'W',       // trp
    'Y',       // tyr
    'V',       // val
//    'B',       // Asx
//    'Z',       // Glx
//    'X'        // Unk
};

const int FitFromPepXML::NUMENTRIES = sizeof(AMINOACIDS);

auto_ptr<FitFromPepXML> FitFromPepXML::create()
{
    return auto_ptr<FitFromPepXML>(new FitFromPepXMLImpl());
}

auto_ptr<FitFromPepXML> FitFromPepXML::create(const char* pepxmlFile,
                                              float tol,
                                              float rtmin, float rtmax,
                                              unsigned limit)
{
    return auto_ptr<FitFromPepXML>(new FitFromPepXMLImpl(pepxmlFile, tol, rtmin, rtmax, limit));
}

auto_ptr<FitFromPepXML> FitFromPepXML::createFromMatrix(const dmatrix& peps,
                                                    const dvector& rts)
{
    return auto_ptr<FitFromPepXML>(new FitFromPepXMLImpl(peps, rts));
}

FitFromPepXMLImpl::FitFromPepXMLImpl()
    : bad_(false), pepxmlFile(NULL),
      limit_(0),
      system(auto_ptr<PeptideRTSystem>(0)),
      extractor(auto_ptr<DataExtractor>(0))
{
}

FitFromPepXMLImpl::FitFromPepXMLImpl(const dmatrix& peps,
                                     const dvector& rts)
    : bad_(false), pepxmlFile(NULL),
      limit_(0),
      system(auto_ptr<PeptideRTSystem>(0)),
      extractor(auto_ptr<DataExtractor>(0))
{
    system = PeptideRTSystem::create(peps, rts);

    fit();
}

FitFromPepXMLImpl::FitFromPepXMLImpl(const char* pepXMLFile, float tol,
                                     float rtmin, float rtmax, 
                                     unsigned limit)
    : bad_(false), pepxmlFile(pepXMLFile),
      tol_(tol), rtmin_(rtmin), rtmax_(rtmax), limit_(limit),
      system(auto_ptr<PeptideRTSystem>(0)),
      extractor(auto_ptr<DataExtractor>(0))
{
    this->pepxmlFile = pepxmlFile;

    //load(pepXMLFile);
}

const dvector FitFromPepXMLImpl::load()
{
    if (pepxmlFile)
        return load(pepxmlFile, limit_);

    return fit();
}

const dvector FitFromPepXMLImpl::load(const char* pepxmlFile, unsigned limit)
{
    bad_ = false;
    this->pepxmlFile = pepxmlFile;
    limit_ = limit;

    extractor = DataExtractor::create(pepxmlFile, tol_, rtmin_, rtmax_);

    /*
     * Ignore amino acids that we don't care about.
     */
    extractor->exclude('B');
    extractor->exclude('Z');
    extractor->exclude('X');

    extractor->parse();
    
    if (extractor.get() != NULL)
    {
        createSystem(extractor->getPeptideList());

        fit();
    }
    else
    {
        bad_ = true;
        throw runtime_error("Error occurred while loading pepxml");
    }

    return lls.solve(system->getPeptideCounts(),
                     system->getRetentionTimes());
}

bool FitFromPepXMLImpl::createSystem (vector<Peptide>& peptideList)
{
    // For each row, sum the occurance of different peptides.
    unsigned size = (limit_ >0 ? std::min((unsigned)peptideList.size(),
                                          limit_) :
                     peptideList.size());

    dmatrix pm(size, NUMENTRIES+1);
    dvector rts(size);

    pm.clear();
    rts.clear();

    for (size_t i= 0;i < size && !bad_; i++)
    {
        const char* rt_str = peptideList[i].getRetentionTime();
        const char* peptide_str = peptideList[i].getPeptide();

        if (rt_str == NULL)
        {
            cerr << "NULL pointer in rt_str" << endl;
            bad_ = true;
            continue;
        }
        
        // Add a row in the matrix and an entry in the vector
        rts(i) = atof(rt_str);
        dvector peptide = countPeptide(peptide_str);

        ublas::matrix_row<dmatrix> mr(pm, i);
        ublas::matrix_row<dmatrix>::iterator loc;
        dvector::iterator val;
        for (val=peptide.begin(),loc=mr.begin();val!=peptide.end();val++,loc++)
        {
            *loc = *val;
        }
    }

    if (!bad_)
        system = PeptideRTSystem::create(pm, rts);

    return bad_;
}

const dvector FitFromPepXMLImpl::fit()
{
    return lls.solve(system->getPeptideCounts(),
                     system->getRetentionTimes());
}

dvector FitFromPepXMLImpl::countPeptide(const char* peptide_str)
{
    dvector countv(NUMENTRIES+1);
    countv.clear();
    countv(NUMENTRIES) = 1;
    
    int size = strlen(peptide_str);

    for (const char* i=peptide_str; i != peptide_str+size; i++)
    {
        bool found = false;
        for (size_t j=0; j<sizeof(AMINOACIDS); j++)
        {
            if (AMINOACIDS[j] == toupper(*i))
            {
                found = true;
                countv(j)+=1;
                break;
            }
        }
        
        if (!found)
            cerr << "No AA for " << *i << endl;
    }

    return countv;
}

}
}
