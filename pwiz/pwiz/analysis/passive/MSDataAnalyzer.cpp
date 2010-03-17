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

#include "MSDataAnalyzer.hpp"


namespace pwiz {
namespace analysis {


using namespace std;


//
// MSDataAnalyzerContainer
//


PWIZ_API_DECL void MSDataAnalyzerContainer::open(const DataInfo& dataInfo) 
{
    for (const_iterator it=begin(); it!=end(); ++it)
    if (it->get())
        (*it)->open(dataInfo);
}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
MSDataAnalyzerContainer::updateRequested(const DataInfo& dataInfo, 
                                         const SpectrumIdentity& spectrumIdentity) const 
{
    // return maximum UpdateRequest of children

    UpdateRequest result = UpdateRequest_None;
    
    for (const_iterator it=begin(); it!=end(); ++it)
    if (it->get())
    {
        UpdateRequest request = (*it)->updateRequested(dataInfo, spectrumIdentity);
        if (result < request)
            result = request;
    }

    return result;
}


PWIZ_API_DECL
void MSDataAnalyzerContainer::update(const DataInfo& dataInfo, 
                                     const Spectrum& spectrum)
{
    // send update only to those children who are ok with it

    for (const_iterator it=begin(); it!=end(); ++it)
    if (it->get() && 
        (*it)->updateRequested(dataInfo, spectrum) >= UpdateRequest_Ok)
        (*it)->update(dataInfo, spectrum);
}


PWIZ_API_DECL void MSDataAnalyzerContainer::close(const DataInfo& dataInfo)
{
    for (const_iterator it=begin(); it!=end(); ++it)
    if (it->get())
        (*it)->close(dataInfo);
}


//
// MSDataAnalyzerDriver
//


PWIZ_API_DECL MSDataAnalyzerDriver::MSDataAnalyzerDriver(MSDataAnalyzer& analyzer)
:   analyzer_(analyzer)
{}


PWIZ_API_DECL
MSDataAnalyzerDriver::Status 
MSDataAnalyzerDriver::analyze(const MSDataAnalyzer::DataInfo& dataInfo,
                              ProgressCallback* progressCallback) const
{
    analyzer_.open(dataInfo);

    size_t iterationsPerCallback = 1;
    if (progressCallback)
        iterationsPerCallback = max(progressCallback->iterationsPerCallback(), size_t(1));

    if (dataInfo.msd.run.spectrumListPtr.get())
    {
        const SpectrumList& spectrumList = *dataInfo.msd.run.spectrumListPtr;
        const size_t size = spectrumList.size();

        for (size_t i=0; i<size; ++i)
        {
            if (progressCallback && 
                (i%iterationsPerCallback)==0 &&
                progressCallback->progress(i, size)==Status_Cancel)
                return Status_Cancel;

            // only send request if analyzer really wants it (more than UpdateRequest_Ok) 

            MSDataAnalyzer::UpdateRequest request = 
                analyzer_.updateRequested(dataInfo, spectrumList.spectrumIdentity(i));

            if (request < MSDataAnalyzer::UpdateRequest_NoBinary) 
                continue;

            // retrieve the spectrum and update the analyzer

            bool getBinaryData = (request == MSDataAnalyzer::UpdateRequest_Full);
            SpectrumPtr spectrum = spectrumList.spectrum(i, getBinaryData);
            analyzer_.update(dataInfo, *spectrum);
        }

        if (progressCallback && progressCallback->progress(size, size)==Status_Cancel)
            return Status_Cancel;
    }

    analyzer_.close(dataInfo);

    return Status_Ok;
}


} // namespace analysis 
} // namespace pwiz

