//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <string>
#include <vector>


namespace pwiz {
namespace msdata {


class PWIZ_API_DECL TextWriter
{
    public:

    /// constructs a TextWriter for MSData types
    /// @param os  The ostream to write to.
    /// @param depth  The number of indentations to prefix to each output line.
    /// @param arrayExampleCount  The number of example values to print for arrays (i.e. m/z and intensity); -1 for unlimited
    TextWriter(std::ostream& os, int depth = 0, int arrayExampleCount = 3)
        :   os_(os),
            depth_(depth),
            arrayExampleCount_(arrayExampleCount < 0 ? std::numeric_limits<size_t>::max()
                                                     : (size_t) arrayExampleCount),
            indent_(depth*2, ' ')
    {}

    TextWriter child() {return TextWriter(os_, depth_+1, arrayExampleCount_);}

    TextWriter& operator()(const std::string& text)
    {
        os_ << indent_ << text << std::endl;
        return *this;
    }

    TextWriter& operator()(const CVParam& cvParam)
    {
        os_ << indent_ << "cvParam: " << cvTermInfo(cvParam.cvid).name;
        if (!cvParam.value.empty())
            os_ << ", " << cvParam.value;
        if (cvParam.units != CVID_Unknown)
            os_ << ", " << cvParam.unitsName();
        os_ << std::endl; 
        return *this;    
    }

    TextWriter& operator()(const UserParam& userParam)
    {
        os_ << indent_ << "userParam: " << userParam.name;
        if (!userParam.value.empty()) os_ << ", " << userParam.value; 
        if (!userParam.type.empty()) os_ << ", " << userParam.type; 
        if (userParam.units != CVID_Unknown) os_ << ", " << cvTermInfo(userParam.units).name;
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


    TextWriter& operator()(const MSData& msd, bool metadata_only=false)
    {
        (*this)("msdata:");
        child()
            ("id: " + msd.id);
        if (!msd.accession.empty())
            child()("accession: " + msd.accession);
        if (!msd.version().empty())
            child()("version: " + msd.version());
        if (!msd.cvs.empty())
            child()("cvList: ", msd.cvs);
        if (!msd.fileDescription.empty())
            child()(msd.fileDescription);
        if (!msd.paramGroupPtrs.empty())
            child()("paramGroupList: ", msd.paramGroupPtrs);
        if (!msd.samplePtrs.empty())
            child()("sampleList: " , msd.samplePtrs);
        if (!msd.softwarePtrs.empty())
            child()("softwareList: ", msd.softwarePtrs);
        if (!msd.scanSettingsPtrs.empty())
            child()("scanSettingsList: ", msd.scanSettingsPtrs);
        if (!msd.instrumentConfigurationPtrs.empty())
            child()("instrumentConfigurationList: ", msd.instrumentConfigurationPtrs);
        if (!msd.dataProcessingPtrs.empty())
            child()("dataProcessingList: ", msd.dataProcessingPtrs);

        if (!msd.run.empty())
            child()(msd.run, metadata_only);

        return *this;
    }
    
    TextWriter& operator()(const CV& cv)
    {
        (*this)("cv:");
        child()
            ("id: " + cv.id)
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
             (*this)("referenceableParamGroupRef: " + (*it)->id);
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

    TextWriter& operator()(const Sample& sample)
    {
        (*this)("sample:");
        child()
            ("id: " + sample.id)
            ("name: " + sample.name)
            (static_cast<const ParamContainer&>(sample));
        return *this;
    }

    TextWriter& operator()(const InstrumentConfiguration& instrumentConfiguration)
    {
        (*this)("instrumentConfiguration:");
        child()
            ("id: " + instrumentConfiguration.id)
            (static_cast<const ParamContainer&>(instrumentConfiguration));
        if (!instrumentConfiguration.componentList.empty())
            child()(instrumentConfiguration.componentList);
        if (instrumentConfiguration.softwarePtr.get() && !instrumentConfiguration.softwarePtr->empty())
            child()("softwareRef: " + instrumentConfiguration.softwarePtr->id);
        return *this;    
    }

    TextWriter& operator()(const ComponentList& componentList)
    {
        (*this)("componentList:");
        for (size_t i=0; i < componentList.size(); ++i)
            child()(componentList[i]);
        return *this;
    }

    TextWriter& operator()(const Component& component)
    {
        switch(component.type)
        {
            case ComponentType_Source:
                (*this)("source: ");
                break;
            case ComponentType_Analyzer:
                (*this)("analyzer: ");
                break;
            case ComponentType_Detector:
                (*this)("detector: ");
                break;
            default:
                break;
        }
        child()
            ("order: " + boost::lexical_cast<std::string>(component.order))
            (static_cast<const ParamContainer&>(component));
        return *this;
    }

    TextWriter& operator()(const Software& software)
    {
        (*this)("software:");
        child()
            ("id: " + software.id)
            ("version: " + software.version)
            (static_cast<const ParamContainer&>(software));
        return *this;
    }

    TextWriter& operator()(const ProcessingMethod& processingMethod)
    {
        (*this)("processingMethod:");
        child()
            ("order: " + boost::lexical_cast<std::string>(processingMethod.order));
        if (processingMethod.softwarePtr.get() && !processingMethod.softwarePtr->empty())
            child()("softwareRef: " + processingMethod.softwarePtr->id);
        child()
            (static_cast<const ParamContainer&>(processingMethod));
        return *this;
    }

    TextWriter& operator()(const DataProcessing& dp)
    {
        (*this)("dataProcessing:");
        child()
            ("id: " + dp.id);
        for_each(dp.processingMethods.begin(), dp.processingMethods.end(), child());
        return *this;
    }
    
    TextWriter& operator()(const Target& target)
    {
        (*this)("target:");
        child()(static_cast<const ParamContainer&>(target));
        return *this;
    }
    
    TextWriter& operator()(const ScanSettings& as)
    {
        (*this)("scanSettings:");
        child()
            ("id: " + as.id);
        for_each(as.targets.begin(), as.targets.end(), child());
        child()("sourceFileList: ", as.sourceFilePtrs);
        return *this;
    }
 
    TextWriter& operator()(const Run& run, bool metadata_only=false)
    {
        (*this)("run:");
        child()("id: " + run.id);
        if (run.defaultInstrumentConfigurationPtr.get())
            child()("defaultInstrumentConfigurationRef: " + run.defaultInstrumentConfigurationPtr->id);
        if (run.samplePtr.get())
            child()("sampleRef: " + run.samplePtr->id);
        if (!run.startTimeStamp.empty())
            child()("startTimeStamp: " + run.startTimeStamp);
        child()(static_cast<const ParamContainer&>(run));
        if (run.defaultSourceFilePtr.get())
            child()("defaultSourceFileRef: " + run.defaultSourceFilePtr->id);
        if (run.spectrumListPtr.get())
            child()(run.spectrumListPtr, metadata_only);
        if (run.chromatogramListPtr.get())
            child()(run.chromatogramListPtr, metadata_only);
        return *this;
    }

    TextWriter& operator()(const SpectrumList& spectrumList, bool metadata_only=false)
    {
        std::string text("spectrumList (" + boost::lexical_cast<std::string>(spectrumList.size()) + " spectra)");
        if (!metadata_only)
            text += ":";

        (*this)(text);

        if (spectrumList.dataProcessingPtr().get())
            child()(*spectrumList.dataProcessingPtr());

        if (!metadata_only)
            for (size_t index=0; index<spectrumList.size(); ++index)
                child()
                    (*spectrumList.spectrum(index, true));
        return *this;
    }

    TextWriter& operator()(const SpectrumListPtr& p, bool metadata_only=false)
    {
        return p.get() ? (*this)(*p, metadata_only) : *this;
    }

    TextWriter& operator()(const ChromatogramList& chromatogramList, bool metadata_only=false)
    {
        std::string text("chromatogramList (" + boost::lexical_cast<std::string>(chromatogramList.size()) + " chromatograms)");
        if (!metadata_only)
            text += ":";

        (*this)(text);

        if (chromatogramList.dataProcessingPtr().get())
            child()(*chromatogramList.dataProcessingPtr());

        if (!metadata_only)
            for (size_t index=0; index<chromatogramList.size(); ++index)
                child()
                    (*chromatogramList.chromatogram(index, true)); 
        return *this;
    }

    TextWriter& operator()(const ChromatogramListPtr& p, bool metadata_only=false)
    {
        return p.get() ? (*this)(*p, metadata_only) : *this;
    }

    TextWriter& operator()(const Spectrum& spectrum)
    {
        (*this)("spectrum:");
        child()
            ("index: " + boost::lexical_cast<std::string>(spectrum.index))
            ("id: " + spectrum.id);
        if (!spectrum.spotID.empty())
            child()("spotID: " + spectrum.spotID);
        if (spectrum.sourceFilePtr.get())
            child()(spectrum.sourceFilePtr);
        child()
            ("defaultArrayLength: " + boost::lexical_cast<std::string>(spectrum.defaultArrayLength))
            (spectrum.dataProcessingPtr)
            (static_cast<const ParamContainer&>(spectrum));
        if (!spectrum.scanList.empty())
            child()(spectrum.scanList);
        if (!spectrum.precursors.empty())
            child()("precursorList: ", spectrum.precursors);
        for_each(spectrum.binaryDataArrayPtrs.begin(), spectrum.binaryDataArrayPtrs.end(), child());
        for_each(spectrum.integerDataArrayPtrs.begin(), spectrum.integerDataArrayPtrs.end(), child());
        return *this;
    }

    TextWriter& operator()(const Chromatogram& chromatogram)
    {
        (*this)("chromatogram:");
        child()
            ("index: " + boost::lexical_cast<std::string>(chromatogram.index))
            ("id: " + chromatogram.id)
            ("defaultArrayLength: " + boost::lexical_cast<std::string>(chromatogram.defaultArrayLength))
            (chromatogram.dataProcessingPtr)
            (static_cast<const ParamContainer&>(chromatogram));
        if (!chromatogram.precursor.empty())
            child()(chromatogram.precursor);
        if (!chromatogram.product.empty())
            child()(chromatogram.product);
        for_each(chromatogram.binaryDataArrayPtrs.begin(), chromatogram.binaryDataArrayPtrs.end(), child());
        for_each(chromatogram.integerDataArrayPtrs.begin(), chromatogram.integerDataArrayPtrs.end(), child());
        return *this;
    }

    TextWriter& operator()(const Scan& scan)
    {
        (*this)("scan:");
        if (!scan.spectrumID.empty()) child()("spectrumID: " + scan.spectrumID);
        if (!scan.externalSpectrumID.empty()) child()("externalSpectrumID: " + scan.externalSpectrumID);
        if (scan.sourceFilePtr) child()(*scan.sourceFilePtr);
        if (scan.instrumentConfigurationPtr.get()) child()(*scan.instrumentConfigurationPtr);
        child()(static_cast<const ParamContainer&>(scan));
        if (!scan.scanWindows.empty())
            child()("scanWindowList: ", scan.scanWindows);
        return *this;
    }

    TextWriter& operator()(const ScanWindow& window)
    {
        (*this)("scanWindow:");
        for_each(window.cvParams.begin(), window.cvParams.end(), child());
        return *this;
    }

    template <typename BinaryDataArrayType>
    TextWriter& writeBinaryDataArray(const BinaryDataArrayType& p)
    {
        if (!p.get() || p->empty()) return *this;
        
        std::stringstream oss;
        oss << "[" << boost::lexical_cast<std::string>(p->data.size()) << "] ";
        oss.precision(12);
        for (size_t i=0; i < arrayExampleCount_ && i < p->data.size(); i++)
            oss << p->data[i] << " ";
        if (p->data.size() > arrayExampleCount_)
            oss << "...";

        (*this)("binaryDataArray:");
        child() (static_cast<const ParamContainer&>(*p));
        if (p->dataProcessingPtr.get() && !p->dataProcessingPtr->empty())
            child()(p->dataProcessingPtr);
        if (!p->data.empty())
            child()("binary: " + oss.str());
        return *this;
    }

    TextWriter& operator()(const BinaryDataArrayPtr& p)
    {
        return writeBinaryDataArray(p);
    }

    TextWriter& operator()(const IntegerDataArrayPtr& p)
    {
        return writeBinaryDataArray(p);
    }

    TextWriter& operator()(const SelectedIon& selectedIon)
    {
        (*this)("selectedIon:");
        child()(static_cast<const ParamContainer&>(selectedIon));
        return *this;
    }

    TextWriter& operator()(const Precursor& precursor)
    {
        (*this)("precursor:");
        child()
            ("spectrumRef: " + precursor.spectrumID)
            (static_cast<const ParamContainer&>(precursor));

        if (!precursor.isolationWindow.empty())
        {
            child()("isolationWindow:");
            child().child()(precursor.isolationWindow);
        }

        if (!precursor.selectedIons.empty())
        { 
            child()("selectedIons:", precursor.selectedIons);
        }

        if (!precursor.activation.empty())
        {
            child()("activation:");
            child().child()(precursor.activation);
        }

        return *this;
    }

    TextWriter& operator()(const Product& product)
    {
        (*this)("product:");

        if (!product.isolationWindow.empty())
        {
            child()("isolationWindow:");
            child().child()(product.isolationWindow);
        }

        return *this;
    }

    TextWriter& operator()(const ScanList& scanList)
    {
        (*this)
            (static_cast<const ParamContainer&>(scanList))
            ("scanList:", scanList.scans);
        return *this;
    }

    // if no other overload matches, assume the object is a shared_ptr of a valid overloaded type
    template<typename object_type>
    TextWriter& operator()(const boost::shared_ptr<object_type>& p)
    {
        return p.get() ? (*this)(*p) : *this;
    }


    private:
    std::ostream& os_;
    int depth_;
    size_t arrayExampleCount_;
    std::string indent_;
};

	
} // namespace msdata
} // namespace pwiz


#endif // _TEXTWRITER_HPP_

