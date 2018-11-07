//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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

#include "SpectrumList_MGF.hpp"
#include "References.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/thread.hpp>


namespace pwiz {
namespace msdata {


using boost::iostreams::stream_offset;
using boost::iostreams::offset_to_position;
using namespace pwiz::util;


namespace {

class SpectrumList_MGFImpl : public SpectrumList_MGF
{
    public:

    SpectrumList_MGFImpl(shared_ptr<std::istream> is, const MSData& msd)
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

    // returns the IndexList for a given TITLE=string
    IndexList findSpotID(const string& titleID) const
    {
        map<string,IndexList>::const_iterator it=titleIDToIndexList_.find(titleID);
        return it!=titleIDToIndexList_.end() ? it->second : IndexList();
    }


    SpectrumPtr spectrum(size_t index, bool getBinaryData) const
    {
        boost::lock_guard<boost::mutex> lock(readMutex);  // lock_guard will unlock mutex when out of scope or when exception thrown (during destruction)
        if (index > index_.size())
            throw runtime_error("[SpectrumList_MGF::spectrum] Index out of bounds");

        // allocate Spectrum object and read it in
        SpectrumPtr result(new Spectrum);
        if (!result.get())
            throw runtime_error("[SpectrumList_MGF::spectrum] Out of memory");

        result->index = index;
        result->sourceFilePosition = index_[index].sourceFilePosition;

        is_->seekg(bio::offset_to_position(result->sourceFilePosition));
        if (!*is_)
            throw runtime_error("[SpectrumList_MGF::spectrum] Error seeking to BEGIN IONS tag");

        parseSpectrum(*result, getBinaryData);

        // resolve any references into the MSData object
        References::resolve(*result, msd_);

        return result;
    }

    private:
    shared_ptr<istream> is_;
    const MSData& msd_;
    vector<SpectrumIdentity> index_;
    map<string, size_t> idToIndex_;
    map<string, IndexList> titleIDToIndexList_;
    mutable boost::mutex readMutex;

    void parseSpectrum(Spectrum& spectrum, bool getBinaryData) const
    {
        // Every MGF spectrum is assumed to be:
        // * MSn spectrum
        // * MS level 2
        // * from a single precursor
        // * a peak list (centroided)
        // * not deisotoped (even though it may actually be, there's no way to tell)

        spectrum.id = "index=" + lexical_cast<string>(spectrum.index);
        spectrum.set(MS_MSn_spectrum);
        spectrum.set(MS_ms_level, 2);
        spectrum.set(MS_centroid_spectrum);

        spectrum.scanList.set(MS_no_combination);
        spectrum.scanList.scans.push_back(Scan());
        Scan& scan = spectrum.scanList.scans[0];

        spectrum.precursors.push_back(Precursor());
        Precursor& precursor = spectrum.precursors.back();
        precursor.selectedIons.push_back(SelectedIon());
        SelectedIon& selectedIon = precursor.selectedIons.back();

        string lineStr;
	    bool inBeginIons = false;
        bool inPeakList = false;
        bool negativePolarity = false;
        double lowMZ = std::numeric_limits<double>::max();
        double highMZ = 0;
        double tic = 0;
        double basePeakMZ = 0;
        double basePeakIntensity = 0;
        spectrum.defaultArrayLength = 0;
        spectrum.setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
        BinaryData<double>& mzArray = spectrum.getMZArray()->data;
        BinaryData<double>& intensityArray = spectrum.getIntensityArray()->data;
	    while (getline(*is_, lineStr))
	    {
            size_t lineBegin = lineStr.find_first_not_of(" \t");
            if (lineBegin == string::npos)
            {
                // Skip blank lines
                continue;
            }
            else if (lineBegin > 0)
            {
                // Trim leading whitespace
                lineStr.erase(0, lineBegin);
            }

            if (!inBeginIons && (lineStr[0] == '#' || lineStr[0] == ';' || lineStr[0] == '!' || lineStr[0] == '/'))
            {
                // Skip comment lines (lines beginning with #;!/ outside of BEGIN IONS)
                continue;
            }
		    if (lineStr.find("BEGIN IONS") == 0)
		    {
			    if (inBeginIons)
			    {
                    throw runtime_error(("[SpectrumList_MGF::parseSpectrum] BEGIN IONS tag found without previous BEGIN IONS being closed at offset " +
                                         lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
			    }
			    inBeginIons = true;
		    }
            else if (lineStr.find("END IONS") == 0)
		    {
			    if (!inBeginIons)
				    throw runtime_error(("[SpectrumList_MGF::parseSpectrum] END IONS tag found without opening BEGIN IONS tag at offset " +
                                         lexical_cast<string>(size_t(is_->tellg())-lineStr.length()-1) + "\n"));
			    inBeginIons = false;
                inPeakList = false;
                break;
            }
            else
            {
                try
                {
                    if (!inPeakList)
                    {
                        size_t delim = lineStr.find('=');
                        if (delim == string::npos)
				        {
					        inPeakList = true;
                        }
                        else
                        {
                            string name = lineStr.substr(0, delim);
                            string value = lineStr.substr(delim+1);
                            if (name == "TITLE")
                            {
                                bal::trim(value);

                                // Some formats omit RTINSECONDS and store the retention time
                                // in the title field instead.
                                double scanTimeMin = getRetentionTimeFromTitle(value);
                                if (scanTimeMin > 0)
                                    scan.set(MS_scan_start_time, scanTimeMin * 60, UO_second);

                                spectrum.set(MS_spectrum_title, value);
                            }
                            else if (name == "PEPMASS")
				            {
                                bal::trim(value);
                                size_t delim2 = value.find(' ');
                                if (delim2 != string::npos)
                                {
                                    selectedIon.set(MS_selected_ion_m_z, value.substr(0, delim2), MS_m_z);
                                    selectedIon.set(MS_peak_intensity, value.substr(delim2+1), MS_number_of_detector_counts);
                                }
                                else
                                    selectedIon.set(MS_selected_ion_m_z, value, MS_m_z);
				            }
                            else if (name == "CHARGE")
				            {
                                bal::trim_if(value, bal::is_any_of(" \t\r"));
                                negativePolarity = bal::ends_with(value, "-");
                                vector<string> charges;
                                bal::split(charges, value, bal::is_any_of(" "));
                                if (charges.size() > 1)
                                {
                                    BOOST_FOREACH(string& charge, charges)
                                        if (charge != "and")
                                        {
                                            bal::trim_if(charge, bal::is_any_of("+-"));
                                            selectedIon.cvParams.push_back(CVParam(MS_possible_charge_state, lexical_cast<int>(charge)));
                                        }
                                }
                                else
                                {
                                    bal::trim_if(value, bal::is_any_of("+-"));
                                    selectedIon.set(MS_charge_state, lexical_cast<int>(value));
                                }
				            }
                            else if (name == "RTINSECONDS")
				            {
                                bal::trim(value);
                                // TODO: handle (multiple) time ranges?
                                double scanTime = lexical_cast<double>(value);
                                scan.set(MS_scan_start_time, scanTime, UO_second);
                            }
                            else if (name == "SCANS")
                            {
                                spectrum.set(MS_peak_list_scans, value);
                            }
                            else if (name == "RAWSCANS")
                            {
                                spectrum.set(MS_peak_list_raw_scans, value);
                            }
                            else
				            {
					            continue; // ignored attribute
				            }
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
                    size_t delim2 = lineStr.find_first_not_of(" \t", delim+1);
     				if(delim2 == string::npos)
					    continue;
                    size_t delim3 = lineStr.find_first_of(" \t\r\n", delim2);
				    if(delim3 == string::npos)
                        delim3 = lineStr.length();

                    double mz = lexical_cast<double>(lineStr.substr(0, delim));
				    double inten = lexical_cast<double>(lineStr.substr(delim2, delim3-delim2));
				    tic += inten;
                    if (inten > basePeakIntensity)
                    {
                        basePeakMZ = mz;
                        basePeakIntensity = inten;
                    }

                    lowMZ = std::min(lowMZ, mz);
                    highMZ = std::max(highMZ, mz);

                    ++spectrum.defaultArrayLength;
                    if (getBinaryData)
                    {
                        mzArray.push_back(mz);
                        intensityArray.push_back(inten);
                    }
                }
            }
        }

        if (!getBinaryData)
            spectrum.binaryDataArrayPtrs.clear();

        spectrum.set(negativePolarity ? MS_negative_scan : MS_positive_scan);
        spectrum.set(MS_lowest_observed_m_z, lowMZ);
        spectrum.set(MS_highest_observed_m_z, highMZ);
        spectrum.set(MS_total_ion_current, tic);
        spectrum.set(MS_base_peak_m_z, basePeakMZ);
        spectrum.set(MS_base_peak_intensity, basePeakIntensity);
    }

    /**
     * Parse the spectrum title to look for retention times.  If there are
     * two times, return the center of the range.  Possible formats to look
     * for are "Elution:<time> min", "RT:<time>min" and "rt=<time>,".
     */
    double getRetentionTimeFromTitle(const string& title) const
    {
        // text to search for preceeding and following time
        const char* startTags[3] = { "Elution:", "RT:", "rt=" };
        const char* secondStartTags[3] = { "to ", NULL, NULL };
        const char* endTags[3] = { "min", "min", "," };

        double firstTime = 0;
        double secondTime = 0;
        for(int format_idx = 0; format_idx < 2; format_idx++)
        {

            size_t position = 0;
            firstTime = getTime(title, startTags[format_idx], 
                                endTags[format_idx], position);
            if (secondStartTags[format_idx] != NULL)
            {
                secondTime = getTime(title, secondStartTags[format_idx], 
                                     endTags[format_idx], position);
            }

            if( firstTime > 0 )
                break;

        } // try another format

        double time = firstTime;
        if( secondTime != 0 )
        {
            time = (firstTime + secondTime) / 2 ;
        }

        return time;
    }

    /**
     * Helper function to parse a double from the given string
     * found between the two tags.  Search for number after position
     * Update position to the end of the parsed double.
     */
    double getTime(const string& title, const char* startTag,
                   const char* endTag, size_t position) const
    {
        size_t start = title.find(startTag, position);
        if( start == string::npos )
            return 0; // not found

        start += strlen(startTag);
        size_t end = title.find(endTag, start);
        string timeStr = title.substr(start, end - start);
        try
        {
            double time = boost::lexical_cast<double>(timeStr);
            position = start;
            return time;
        }
        catch(...)
        {
            return 0;
        }
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
                curIdentityItr->id = "index=" + lexical_cast<string>(index_.size()-1);
			    curIdentityItr->sourceFilePosition = size_t(is_->tellg())-lineStr.length()-1;
                curIdToIndexItr = idToIndex_.insert(pair<string, size_t>(curIdentityItr->id, index_.size()-1)).first;
			    inBeginIons = true;
		    }
            else if (lineStr.find("TITLE=") == 0)
	    {
                // if a title is found, use it as the id in the index used by findSpotID
	        string title = lineStr.substr(6);
                bal::trim(title);
		titleIDToIndexList_[title].push_back(index_.size()-1);
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


} // namespace


SpectrumListPtr SpectrumList_MGF::create(boost::shared_ptr<std::istream> is,
                         const MSData& msd)
{
    return SpectrumListPtr(new SpectrumList_MGFImpl(is, msd));
}


} // namespace msdata
} // namespace pwiz
