//
// DefaultReaderList.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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

#include "utility/misc/Filesystem.hpp"
#include "utility/misc/String.hpp"
#include "utility/misc/Stream.hpp"
#include "DefaultReaderList.hpp"
#include "SpectrumList_mzXML.hpp"
#include "Serializer_mzML.hpp"
#include "Serializer_mzXML.hpp"
#include "References.hpp"


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;


namespace {


class Reader_mzML : public Reader
{
    public:

    virtual bool accept(const std::string& filename, const std::string& head) const
    {
         istringstream iss(head); 
         return type(iss) != Type_Unknown; 
    }

    virtual void read(const std::string& filename, const std::string& head, MSData& result) const
    {
        shared_ptr<istream> is(new ifstream(filename.c_str(), ios::binary));
        if (!is.get() || !*is)
            throw runtime_error(("[MSDataFile::Reader_mzML] Unable to open file " + filename).c_str());

        switch (type(*is))
        {
            case Type_mzML:
            {
                Serializer_mzML::Config config;
                config.indexed = false;
                Serializer_mzML serializer(config);
                serializer.read(is, result);
                break;
            }
            case Type_mzML_Indexed:
            {
                Serializer_mzML serializer;
                serializer.read(is, result);
                break;
            }
            case Type_Unknown:
            default:
            {
                throw runtime_error("[MSDataFile::Reader_mzML] This isn't happening."); 
            }
        }
    }

    private:

    enum Type { Type_mzML, Type_mzML_Indexed, Type_Unknown }; 

    Type type(istream& is) const
    {
        is.seekg(0);

        string buffer;
        is >> buffer;

        if (buffer != "<?xml")
            return Type_Unknown;
            
        getline(is, buffer);
        is >> buffer; 

        if (buffer == "<indexedmzML")
            return Type_mzML_Indexed;
        else if (buffer == "<mzML")
            return Type_mzML;
        else
            return Type_Unknown;
    }
};


class Reader_mzXML : public Reader
{
    virtual bool accept(const std::string& filename, const std::string& head) const
    {
        istringstream iss(head); 

        string buffer;
        iss >> buffer;

        if (buffer != "<?xml") return false;

        getline(iss, buffer);
        iss >> buffer; 

        return (buffer=="<mzXML" || buffer=="<msRun");
    }

    virtual void read(const std::string& filename, const std::string& head, MSData& result) const
    {
        shared_ptr<istream> is(new ifstream(filename.c_str(), ios::binary));
        if (!is.get() || !*is)
            throw runtime_error(("[MSDataFile::Reader_mzXML] Unable to open file " + filename).c_str());

        try
        {
            // assume there is a scan index
            Serializer_mzXML serializer;
            serializer.read(is, result);
            return;
        }
        catch (SpectrumList_mzXML::index_not_found&)
        {}

        // error looking for index -- try again, but generate index 
        is->seekg(0);
        Serializer_mzXML::Config config;
        config.indexed = false;
        Serializer_mzXML serializer(config);
        serializer.read(is, result);
        return;
    }
};


/// implementation of SpectrumList, backed by an MGF stream
class PWIZ_API_DECL SpectrumList_MGF : public SpectrumList
{
    public:

    SpectrumList_MGF(boost::shared_ptr<std::istream> is, const MSData& msd)
        :   is_(is), msd_(msd)
    {
        createIndex();
    }

    size_t size() const {return index_.size();}
    
    const SpectrumIdentity& spectrumIdentity(size_t index) const
    {
        return index_[index];
    }

    size_t find(const string& id) const
    {
        map<string, size_t>::const_iterator it = idToIndex_.find(id);
        return it != idToIndex_.end() ? it->second : size();
    }

    size_t findNative(const string& nativeID) const
    {
        size_t index;
        try
        {
            index = lexical_cast<size_t>(nativeID);
        }
        catch (boost::bad_lexical_cast&)
        {
            throw runtime_error("[SpectrumList_MGF::findNative] invalid nativeID format (expected a positive integer)");
        }

        if (index < size())
            return index;
        else
            return size();
    }

    SpectrumPtr spectrum(size_t index, bool getBinaryData) const
    {
        if (index > index_.size())
            throw runtime_error("[SpectrumList_MGF::spectrum] Index out of bounds");

        // returned cached Spectrum if possible
        if (!getBinaryData && spectrumCache_.size() > index && spectrumCache_[index].get())
            return spectrumCache_[index];

        // allocate Spectrum object and read it in
        SpectrumPtr result(new Spectrum);
        if (!result.get())
            throw runtime_error("[SpectrumList_MGF::spectrum] Out of memory");

        result->index = index;
        result->nativeID = lexical_cast<string>(index);

        is_->seekg(bio::offset_to_position(index_[index].sourceFilePosition));
        if (!*is_)
            throw runtime_error("[SpectrumList_MGF::spectrum] Error seeking to BEGIN IONS tag");

        parseSpectrum(*result, getBinaryData);

        if (!getBinaryData)
        {
            if (spectrumCache_.size() <= index)
                spectrumCache_.resize(index+1);
            spectrumCache_[index] = result;
        }

        // resolve any references into the MSData object
        References::resolve(*result, msd_);

        return result;
    }

    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    vector<SpectrumIdentity> index_;
    map<string, size_t> idToIndex_;
    mutable vector<SpectrumPtr> spectrumCache_;

    void parseSpectrum(Spectrum& spectrum, bool getBinaryData) const
    {
        // Every MGF spectrum is assumed to be:
        // * MSn spectrum
        // * MS level 2
        // * from a single precursor
        // * a peak list (centroided)
        // * not deisotoped (even though it may actually be, there's no way to tell)

        spectrum.set(MS_MSn_spectrum);
        spectrum.set(MS_ms_level, 2);
        spectrum.spectrumDescription.set(MS_centroid_mass_spectrum);
        spectrum.spectrumDescription.precursors.push_back(Precursor());
        Precursor& precursor = spectrum.spectrumDescription.precursors.back();
        precursor.selectedIons.push_back(SelectedIon());
        SelectedIon& selectedIon = precursor.selectedIons.back();

        string lineStr;
	    bool inBeginIons = false;
        bool inPeakList = false;
        double tic = 0;
        vector<MZIntensityPair> peaks;
	    while (getline(*is_, lineStr))
	    {
		    if (lineStr.find("BEGIN IONS") == 0)
		    {
			    if (inBeginIons)
			    {
                    throw runtime_error(("[SpectrumList_MGF::parseSpectrum] BEGIN IONS tag found without previous BEGIN IONS being closed at offset " +
                                         lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
			    }
			    inBeginIons = true;
		    }
            else if (lineStr.find("TITLE=") == 0)
		    {
                // if a title is found, use it as the id instead of the index
			    spectrum.id = lineStr.substr(6);
                bal::trim(spectrum.id);
		    }
            else if (lineStr.find("END IONS") == 0)
		    {
			    if (!inBeginIons)
				    throw runtime_error(("[SpectrumList_MGF::parseSpectrum] END IONS tag found without opening BEGIN IONS tag at offset " +
                                         lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
			    inBeginIons = false;
                inPeakList = false;

                if (spectrum.id.empty())
                    spectrum.id = spectrum.nativeID;
                break;
            }
            else
            {
                try
                   {
                    if (!inPeakList)
                    {
                        if (lineStr.find("PEPMASS=") == 0)
				        {
					        string pepMassStr = lineStr.substr(8);
                            bal::trim(pepMassStr);
                            double mz = lexical_cast<double>(pepMassStr);
                            selectedIon.set(MS_m_z, mz);
				        }
                        else if (lineStr.find("CHARGE=") == 0)
				        {
					        string pepChargeStr = lineStr.substr(7);
                            bal::trim_if(pepChargeStr, bal::is_any_of("+- \t\r"));
					        int charge = lexical_cast<int>(pepChargeStr);
                            selectedIon.set(MS_charge_state, charge);
				        }
                        else if (lineStr.find("RTINSECONDS=") == 0)
				        {
					        string rtStr = lineStr.substr(12);
                            bal::trim(rtStr);
                            // TODO: handle (multiple) time ranges?
                            double scanTime = lexical_cast<double>(rtStr);
                            spectrum.spectrumDescription.scan.set(MS_scan_time, scanTime, UO_second);
                        }
                        else if(lineStr.find('=') != string::npos)
				        {
					        continue; // ignored attribute
				        }
                        else
			            {
				            inPeakList = true;
			            }
                    }
                }
                catch(bad_lexical_cast&)
                {
                    throw runtime_error(("[SpectrumList_MGF::parseSpectrum] Error parsing line at offset " +
                                        lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + ": " + lineStr + "\n"));
                }

                if (inPeakList)
                {
                    // always parse the peaks (intensity must be summed to build TIC)
                    size_t delim = lineStr.find_first_of(" \t");
				    if(delim == string::npos)
					    continue;
				    size_t delim2 = lineStr.find_first_of("\r\n", delim+1);
				    if(delim2 == string::npos)
					    continue;

				    double inten = lexical_cast<double>(lineStr.substr(delim+1, delim2-delim-1));
				    tic += inten;

                    if (getBinaryData)
				    {
					    double mz = lexical_cast<double>(lineStr.substr(0, delim));
                        peaks.push_back(MZIntensityPair(mz, inten));
				    }
                }
            }
        }

        spectrum.spectrumDescription.set(MS_total_ion_current, tic);
        if (getBinaryData)
            spectrum.setMZIntensityPairs(peaks);
    }

    void createIndex()
    {
        string lineStr;
	    size_t lineCount = 0;
	    bool inBeginIons = false;
        vector<SpectrumIdentity>::iterator curIdentityItr;
        map<string, size_t>::iterator curIdToIndexItr;

	    while (getline(*is_, lineStr))
	    {
		    ++lineCount;
		    if (lineStr.find("BEGIN IONS") == 0)
		    {
			    if (inBeginIons)
			    {
                    throw runtime_error(("[SpectrumList_MGF::createIndex] BEGIN IONS tag found without previous BEGIN IONS being closed at line " +
                                         lexical_cast<string>(lineCount) + "\n"));

			    }
                index_.push_back(SpectrumIdentity());
			    curIdentityItr = index_.begin() + (index_.size()-1);
                curIdentityItr->index = index_.size()-1;
                curIdentityItr->id = lexical_cast<string>(index_.size()-1);
                curIdentityItr->nativeID = index_.size()-1;
			    curIdentityItr->sourceFilePosition = size_t(is_->tellg())-lineStr.length()-1;
                curIdToIndexItr = idToIndex_.insert(pair<string, size_t>(curIdentityItr->id, index_.size()-1)).first;
			    inBeginIons = true;
		    }
            else if (lineStr.find("TITLE=") == 0)
		    {
                // if a title is found, use it as the id instead of the index
			    curIdentityItr->id = lineStr.substr(6);
                bal::trim(curIdentityItr->id);
                idToIndex_.erase(curIdToIndexItr);
                curIdToIndexItr = idToIndex_.insert(pair<string, size_t>(curIdentityItr->id, index_.size()-1)).first;
			    
		    }
            else if (lineStr.find("END IONS") == 0)
		    {
			    if (!inBeginIons)
				    throw runtime_error(("[SpectrumList_MGF::createIndex] END IONS tag found without opening BEGIN IONS tag at line " +
                                         lexical_cast<string>(lineCount) + "\n"));
			    inBeginIons = false;
            }
        }
        is_->clear();
        is_->seekg(0);
    }
};


class Reader_MGF : public Reader
{
    virtual bool accept(const string& filename, const string& head) const
    {
        return (bal::to_lower_copy(bfs::extension(filename)) == ".mgf");
    }

    virtual void read(const string& filename, const string& head, MSData& result) const
    {
        shared_ptr<istream> is(new ifstream(filename.c_str(), ios::binary));
        if (!is.get() || !*is)
            throw runtime_error(("[Reader_MGF::read] Unable to open file " + filename));

        result.fileDescription.fileContent.set(MS_MSn_spectrum);
        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = "MGF1";
        bfs::path p(filename);
        sourceFile->name = p.leaf();
        sourceFile->location = string("file://") + bfs::complete(p.branch_path()).string();
        result.fileDescription.sourceFilePtrs.push_back(sourceFile);
        result.run.id = "Run1";
        result.run.spectrumListPtr = SpectrumListPtr(new SpectrumList_MGF(is, result));
        result.run.chromatogramListPtr = ChromatogramListPtr(new ChromatogramListSimple);
        return;
    }
};


} // namespace


/// default Reader list
PWIZ_API_DECL DefaultReaderList::DefaultReaderList()
{
    push_back(ReaderPtr(new Reader_mzML));
    push_back(ReaderPtr(new Reader_mzXML));
    push_back(ReaderPtr(new Reader_MGF));
}


} // namespace msdata
} // namespace pwiz


