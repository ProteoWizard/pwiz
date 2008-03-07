//
// TextWriter.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#ifndef _TEXTWRITER_HPP_
#define _TEXTWRITER_HPP_


#include "MSData.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <string>
#include <vector>


namespace pwiz {
namespace msdata {


class TextWriter
{
    public:

    TextWriter(std::ostream& os, int depth = 0)
    :   os_(os), depth_(depth), indent_(depth*2, ' ')
    {}

    TextWriter child() {return TextWriter(os_, depth_+1);}

    TextWriter& operator()(const std::string& text)
    {
        os_ << indent_ << text << std::endl;
        return *this;
    }

    TextWriter& operator()(const CVParam& cvParam)
    {
        os_ << indent_ << "cvParam: " << cvinfo(cvParam.cvid).name;
        if (!cvParam.value.empty())
            os_ << ", " << cvParam.value;
        os_ << std::endl; 
        return *this;    
    }

    TextWriter& operator()(const UserParam& userParam)
    {
        os_ << indent_ << "userParam: " << userParam.name;
        if (!userParam.value.empty()) os_ << ", " << userParam.value; 
        if (!userParam.type.empty()) os_ << ", " << userParam.type; 
        os_ << std::endl; 
        return *this;    
    }

    template<typename object_type>
    TextWriter& operator()(const std::string& label, const std::vector<object_type>& v)
    {
        (*this)(label);
        for_each(v.begin(), v.end(), child());
        return *this;
    }

    TextWriter& operator()(const MSData& msd)
    {
        (*this)("msdata:");
        child()
            ("id: " + msd.id);
        if (!msd.accession.empty())
            child()("accession: " + msd.accession);
        if (!msd.version.empty())
            child()("version: " + msd.version);
        if (!msd.cvs.empty())
            child()("cvList: ", msd.cvs);
        if (!msd.fileDescription.empty())
            child()(msd.fileDescription);
        if (!msd.paramGroupPtrs.empty())
            child()("paramGroupList: ", msd.paramGroupPtrs);
        if (!msd.samplePtrs.empty())
            child()("sampleList: " , msd.samplePtrs);
        if (!msd.instrumentPtrs.empty())
            child()("instrumentList: ", msd.instrumentPtrs);
        if (!msd.softwarePtrs.empty())
            child()("softwareList: ", msd.softwarePtrs);
        if (!msd.dataProcessingPtrs.empty())
            child()("dataProcessingList: ", msd.dataProcessingPtrs);
        if (!msd.run.empty())
            child()(msd.run);
        return *this;
    }
    
    TextWriter& operator()(const CV& cv)
    {
        (*this)("cv:");
        child()
            ("cvLabel: " + cv.cvLabel)
            ("fullName: " + cv.fullName)
            ("version: " + cv.version)
            ("URI: " + cv.URI);
        return *this;
    }

    TextWriter& operator()(const FileDescription& fd)
    {
        (*this)("fileDescription:");
        child()
            (fd.fileContent)
            ("sourceFileList: ", fd.sourceFilePtrs);
        for_each(fd.contacts.begin(), fd.contacts.end(), child()); 
        return *this;
    }

    TextWriter& operator()(const ParamContainer& paramContainer)
    {
        for (std::vector<ParamGroupPtr>::const_iterator it=paramContainer.paramGroupPtrs.begin();
             it!=paramContainer.paramGroupPtrs.end(); ++it)
             (*this)("paramGroupRef: " + (*it)->id);
        for_each(paramContainer.cvParams.begin(), paramContainer.cvParams.end(), *this);
        for_each(paramContainer.userParams.begin(), paramContainer.userParams.end(), *this);
        return *this;
    }

    TextWriter& operator()(const FileContent& fileContent)
    {
        (*this)("fileContent:");
        child()(static_cast<const ParamContainer&>(fileContent));
        return *this;
    }

    TextWriter& operator()(const SourceFile& sf)
    {
        (*this)("sourceFile:");
        child()
            ("id: " + sf.id) 
            ("name: " + sf.name) 
            ("location: " + sf.location)
            (static_cast<const ParamContainer&>(sf));
        return *this;
    }

    TextWriter& operator()(const SourceFilePtr& p)
    {
        if (!p.get()) return *this;
        return (*this)(*p);
    }

    TextWriter& operator()(const Contact& contact)
    {
        (*this)("contact:");
        child()(static_cast<const ParamContainer&>(contact));
        return *this;
    }

    TextWriter& operator()(const ParamGroup& paramGroup)
    {
        (*this)("paramGroup:");
        child()
            ("id: " + paramGroup.id)
            (static_cast<const ParamContainer&>(paramGroup));
        return *this;
    }

    TextWriter& operator()(const ParamGroupPtr& p)
    {
        if (!p.get()) return *this;
        return (*this)(*p);
    }

    TextWriter& operator()(const SamplePtr& p)
    {
        if (!p.get()) return *this;
        (*this)("sample:");
        child()
            ("id: " + p->id)
            ("name: " + p->name)
            (static_cast<const ParamContainer&>(*p));
        return *this;
    }

    TextWriter& operator()(const Instrument& instrument)
    {
        (*this)("instrument:");
        child()
            ("id: " + instrument.id)
            (static_cast<const ParamContainer&>(instrument));
        if (!instrument.componentList.empty())
            child()(instrument.componentList);
        if (instrument.softwarePtr.get() && !instrument.softwarePtr->empty())
            child()("instrumentSoftwareRef: " + instrument.softwarePtr->id);
        return *this;    
    }

    TextWriter& operator()(const InstrumentPtr& p)
    {
        return p.get() ? (*this)(*p) : *this;
    }

    TextWriter& operator()(const ComponentList& componentList)
    {
        (*this)("componentList:");
        if (!componentList.source.empty())
            child()(componentList.source);
        if (!componentList.analyzer.empty())
            child()(componentList.analyzer);
        if (!componentList.detector.empty())
            child()(componentList.detector);
        return *this;
    }

    TextWriter& operator()(const Component& component, const std::string& label = "component: ")
    {
        (*this)(label);
        child()
            ("order: " + boost::lexical_cast<std::string>(component.order))
            (static_cast<const ParamContainer&>(component));
        return *this;
    }

    TextWriter& operator()(const Source& source)
    {
        return (*this)(static_cast<const Component&>(source), "source: ");
    }

    TextWriter& operator()(const Analyzer& analyzer) 
    {
        return (*this)(static_cast<const Component&>(analyzer), "analyzer: ");
    }

    TextWriter& operator()(const Detector& detector)
    {
        return (*this)(static_cast<const Component&>(detector), "detector: ");
    }

    TextWriter& operator()(const Software& software)
    {
        (*this)("software:");
        child()
            ("id: " + software.id)
            (software.softwareParam)
            ("version: " + software.softwareParamVersion);
        return *this;
    }

    TextWriter& operator()(const SoftwarePtr& p)
    {
        return p.get() ? (*this)(*p) : *this;
    }

    TextWriter& operator()(const DataProcessing& dp)
    {
        (*this)("dataProcessing:");
        child()
            ("id: " + dp.id);
        if (dp.softwarePtr.get() && !dp.softwarePtr->empty())
            child()("softwareRef: " + dp.softwarePtr->id);
        for_each(dp.processingMethods.begin(), dp.processingMethods.end(), child());
        return *this;
    }

    TextWriter& operator()(const DataProcessingPtr& p)
    {
        return p.get() ? (*this)(*p) : *this;
    }

    TextWriter& operator()(const ProcessingMethod& processingMethod)
    {
        (*this)("processingMethod:");
        child()
            ("order: " + boost::lexical_cast<std::string>(processingMethod.order))
            (static_cast<const ParamContainer&>(processingMethod));
        return *this;
    }

    TextWriter& operator()(const Run& run)
    {
        (*this)("run:");
        child()("id: " + run.id);
        if (run.instrumentPtr.get())
            child()("instrumentRef: " + run.instrumentPtr->id);
        if (run.samplePtr.get())
            child()("sampleRef: " + run.samplePtr->id);
        if (!run.startTimeStamp.empty())
            child()("startTimeStamp: " + run.startTimeStamp);
        child()(static_cast<const ParamContainer&>(run));
        if (!run.sourceFilePtrs.empty())
        {
            child()("sourceFileRefList: ");
            for (std::vector<SourceFilePtr>::const_iterator it=run.sourceFilePtrs.begin();
                 it!=run.sourceFilePtrs.end(); ++it)
                 child().child()("sourceFileRef: " + (*it)->id);
        }
        if (run.spectrumListPtr.get())
            child()(run.spectrumListPtr);
        return *this;
    }

    TextWriter& operator()(const SpectrumList& spectrumList)
    {
        (*this)("spectrumList:");
        for (size_t index=0; index<spectrumList.size(); ++index)
            child()
                (*spectrumList.spectrum(index, true)); 
        return *this;
    }

    TextWriter& operator()(const SpectrumListPtr& p)
    {
        return p.get() ? (*this)(*p) : *this;
    }

    TextWriter& operator()(const Spectrum& spectrum)
    {
        (*this)("spectrum:");
        child()
            ("index: " + boost::lexical_cast<std::string>(spectrum.index))
            ("id: " + spectrum.id)
            ("nativeID: " + boost::lexical_cast<std::string>(spectrum.nativeID))
            ("defaultArrayLength: " + boost::lexical_cast<std::string>(spectrum.defaultArrayLength))
            (spectrum.dataProcessingPtr)
            (static_cast<const ParamContainer&>(spectrum));
        if (!spectrum.spectrumDescription.empty())
            child()(spectrum.spectrumDescription);
        for_each(spectrum.binaryDataArrayPtrs.begin(), spectrum.binaryDataArrayPtrs.end(), child()); 
        return *this;
    }

    TextWriter& operator()(const SpectrumDescription& spectrumDescription)
    {
        (*this)("spectrumDescription:");
        child()(static_cast<const ParamContainer&>(spectrumDescription));

        if (!spectrumDescription.acquisitionList.empty())
            child()(spectrumDescription.acquisitionList);

        if (!spectrumDescription.precursors.empty())
            child()("precursorList: ", spectrumDescription.precursors);

        if (!spectrumDescription.scan.empty())
            child()(spectrumDescription.scan);

        return *this;
    }

    TextWriter& operator()(const Scan& scan)
    {
        (*this)("scan:");
        if (scan.instrumentPtr.get()) child()(*scan.instrumentPtr);
        child()(static_cast<const ParamContainer&>(scan));
        if (!scan.selectionWindows.empty())
            child()("selectionWindowList: ", scan.selectionWindows);
        return *this;
    }

    TextWriter& operator()(const SelectionWindow& window)
    {
        (*this)("selectionWindow:");
        for_each(window.cvParams.begin(), window.cvParams.end(), child());
        return *this;
    }

    TextWriter& operator()(const BinaryDataArrayPtr& p)
    {
        if (!p.get() || p->empty()) return *this;
        
        std::stringstream oss;
        oss << "[" << boost::lexical_cast<std::string>(p->data.size()) << "] ";
        oss.precision(12);
        for (unsigned int i=0; i<3 && i<p->data.size(); i++)
            oss << p->data[i] << " ";
        oss << "...";

        (*this)("binaryDataArray:");
        child() (static_cast<const ParamContainer&>(*p));
        if (p->dataProcessingPtr.get() && !p->dataProcessingPtr->empty())
            child()(p->dataProcessingPtr);
        if (!p->data.empty())
            child()("binary: " + oss.str());
        return *this;
    }

    TextWriter& operator()(const Precursor& precursor)
    {
        (*this)("precursor:");
        child()
            ("spectrumRef: " + precursor.spectrumID)
            (static_cast<const ParamContainer&>(precursor));

        if (!precursor.ionSelection.empty())
        { 
            child()("ionSelection:");
            child().child()(precursor.ionSelection);
        }

        if (!precursor.activation.empty())
        {
            child()("activation:");
            child().child()(precursor.activation);
        }

        return *this;
    }

    TextWriter& operator()(const Acquisition& acquisition)
    {
        (*this)("acquisition:");
        child()
            ("number: " + boost::lexical_cast<std::string>(acquisition.number));
        if (acquisition.sourceFilePtr.get())
            child()("sourceFileRef: " + acquisition.sourceFilePtr->id);
        if (!acquisition.spectrumID.empty())
            child()("spectrumRef: " + acquisition.spectrumID);
        child()
            (static_cast<const ParamContainer&>(acquisition));
        return *this;
    }

    TextWriter& operator()(const AcquisitionList& acquisitionList)
    {
        (*this)("acquisitionList:", acquisitionList.acquisitions);
        return *this;
    }

    private:
    std::ostream& os_;
    int depth_;
    std::string indent_;
};

	
} // namespace msdata
} // namespace pwiz


#endif // _TEXTWRITER_HPP_

