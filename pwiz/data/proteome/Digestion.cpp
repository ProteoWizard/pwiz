//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "Digestion.hpp"
#include "Peptide.hpp"
#include "AminoAcid.hpp"
#include "pwiz/utility/misc/CharIndexedVector.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "boost/utility/singleton.hpp"


namespace pwiz {
namespace proteome {


using namespace std;
using namespace pwiz::cv;
using namespace pwiz::util;
using boost::shared_ptr;

#undef SECURE_SCL


PWIZ_API_DECL
DigestedPeptide::DigestedPeptide(const string& sequence)
:   Peptide(sequence)
{
}

PWIZ_API_DECL
DigestedPeptide::DigestedPeptide(const char* sequence)
:   Peptide(sequence)
{
}

PWIZ_API_DECL
DigestedPeptide::DigestedPeptide(std::string::const_iterator begin,
                                 std::string::const_iterator end,
                                 size_t offset,
                                 size_t missedCleavages,
                                 bool NTerminusIsSpecific,
                                 bool CTerminusIsSpecific)
:   Peptide(begin, end),
    offset_(offset),
    missedCleavages_(missedCleavages),
    NTerminusIsSpecific_(NTerminusIsSpecific),
    CTerminusIsSpecific_(CTerminusIsSpecific)
{
}

PWIZ_API_DECL
DigestedPeptide::DigestedPeptide(std::string::const_iterator begin,
                                 std::string::const_iterator end,
                                 size_t offset,
                                 size_t missedCleavages,
                                 bool NTerminusIsSpecific,
                                 bool CTerminusIsSpecific, 
				 std::string nTermPrefix,
				 std::string cTermSuffix )
:   Peptide(begin, end),
    offset_(offset),
    missedCleavages_(missedCleavages),
    NTerminusIsSpecific_(NTerminusIsSpecific),
    CTerminusIsSpecific_(CTerminusIsSpecific),
    nTermPrefix_(nTermPrefix),
    cTermSuffix_(cTermSuffix)
{
}


PWIZ_API_DECL DigestedPeptide::DigestedPeptide(const DigestedPeptide& other)
:   Peptide(other),
    offset_(other.offset_),
    missedCleavages_(other.missedCleavages_),
    NTerminusIsSpecific_(other.NTerminusIsSpecific_),
    CTerminusIsSpecific_(other.CTerminusIsSpecific_),
    nTermPrefix_(other.nTermPrefix_),
    cTermSuffix_(other.cTermSuffix_)
{
}

PWIZ_API_DECL DigestedPeptide& DigestedPeptide::operator=(const DigestedPeptide& rhs)
{
    Peptide::operator=(rhs);
    offset_ = rhs.offset_;
    missedCleavages_ = rhs.missedCleavages_;
    NTerminusIsSpecific_ = rhs.NTerminusIsSpecific_;
    CTerminusIsSpecific_ = rhs.CTerminusIsSpecific_;
    nTermPrefix_ = rhs.nTermPrefix_;
    cTermSuffix_ = rhs.cTermSuffix_;
    return *this;
}

PWIZ_API_DECL DigestedPeptide::~DigestedPeptide()
{
}

PWIZ_API_DECL size_t DigestedPeptide::offset() const
{
    return offset_;
}

PWIZ_API_DECL size_t DigestedPeptide::missedCleavages() const
{
    return missedCleavages_;
}

PWIZ_API_DECL size_t DigestedPeptide::specificTermini() const
{
    return (size_t) NTerminusIsSpecific_ + (size_t) CTerminusIsSpecific_;
}

PWIZ_API_DECL bool DigestedPeptide::NTerminusIsSpecific() const
{
    return NTerminusIsSpecific_;
}

PWIZ_API_DECL bool DigestedPeptide::CTerminusIsSpecific() const
{
    return CTerminusIsSpecific_;
}

PWIZ_API_DECL std::string DigestedPeptide::nTermPrefix() const
{
    return nTermPrefix_;
}

PWIZ_API_DECL std::string DigestedPeptide::cTermSuffix() const
{
    return cTermSuffix_;
}




PWIZ_API_DECL
Digestion::Config::Config(int maximumMissedCleavages,
                          //double minimumMass,
                          //double maximumMass,
                          int minimumLength,
                          int maximumLength,
                          Specificity minimumSpecificity)
:   maximumMissedCleavages(maximumMissedCleavages),
    //minimumMass(minimumMass), maximumMass(maximumMass),
    minimumLength(minimumLength), maximumLength(maximumLength),
    minimumSpecificity(minimumSpecificity)
{
}


class Digestion::Motif::Impl
{
    public:
    Impl(const string& motif)
        :   motif_(motif)
    {
        parseMotif();
    }

    typedef CharIndexedVector<bool> IsValidAA;

    inline bool testSite(const string& sequence, int offset) const
    {
        bool test = true;

        // start at offset and work toward the sequence's N terminus;
        // if testing at the N terminus (offset = -1), test '{'
        {
            vector<IsValidAA>::const_reverse_iterator filterItr;
            int testOffset = offset;
            for (filterItr = nFilters_.rbegin();
                 test && testOffset >= 0 && filterItr != nFilters_.rend();
                 --testOffset, ++filterItr)
            {
                test = (*filterItr)[sequence[testOffset]];
            }
            if (testOffset < 0 && test && filterItr != nFilters_.rend())
                return (*filterItr)['{'];
        }

        if (!test)
            return false;

        // start at offset+1 and work toward the sequence's C terminus;
        // if testing at the C terminus (offset = length-1), test '}'
        {
            vector<IsValidAA>::const_iterator filterItr;
            int end = (int) sequence.length();
            int testOffset = offset+1;
            for (filterItr = cFilters_.begin();
                 test && testOffset < end && filterItr != cFilters_.end();
                 ++testOffset, ++filterItr)
            {
                test = (*filterItr)[sequence[testOffset]];
            }
            if (testOffset == end && test && filterItr != cFilters_.end())
                return (*filterItr)['}'];
        }

        return test;
    }

    private:
    inline void parseMotif()
    {
        vector<char> multiResidueBlock;
		int multiResidueBlockMode = 0; // 0=off 1=contained residues are included 2=contained residues are excluded
        bool parsedSiteDelimiter = false;
        IsValidAA* pFilter;

		for (size_t i=0, end=motif_.size(); i < end; ++i)
		{
			switch (motif_[i])
			{
				case '[':
					// start multi residue block
					if (multiResidueBlockMode > 0)
						throw runtime_error("[Digestion::Motif::Impl::parseMotif()] Invalid nested multi-residue block opening bracket in motif \"" + motif_ + "\"");
                    else if (i+1 == end)
                        throw runtime_error("[Digestion::Motif::Impl::parseMotif()] Mismatched multi-residue block opening bracket at end of motif \"" + motif_ + "\"");

					multiResidueBlockMode = ( motif_[i+1] == '^' ? 2 : 1 );
                    if (multiResidueBlockMode == 2)
                        ++i; // skip '^'
					break;

				case ']':
					// close multi residue block
					if (multiResidueBlockMode == 0)
                        throw runtime_error("[Digestion::Motif::Impl::parseMotif()] Mismatched multi-residue block closing bracket in motif \"" + motif_ + "\"");
                    (parsedSiteDelimiter ? cFilters_ : nFilters_).push_back(IsValidAA());
                    pFilter = &(parsedSiteDelimiter ? cFilters_ : nFilters_).back();

					if (multiResidueBlockMode == 2)
					{
                        // all residues are valid by default
                        std::fill(pFilter->begin(), pFilter->end(), true);

                        for (size_t j=0; j < multiResidueBlock.size(); ++j)
                            motif_[multiResidueBlock[j]] = false;
					}
                    else
					{
                        // all residues are invalid by default
                        std::fill(pFilter->begin(), pFilter->end(), false);

						for (size_t j=0; j < multiResidueBlock.size(); ++j)
							(*pFilter)[multiResidueBlock[j]] = true;
					}
				
					multiResidueBlockMode = 0;
					multiResidueBlock.clear();
					break;

                case '|':
					// set last block as the modification site
					if( multiResidueBlockMode > 0 )
						throw runtime_error("[Digestion::Motif::Impl::parseMotif()] Digestion site specifier ('|') is invalid inside a multi-residue block in motif \"" + motif_ + "\"");
					else
						parsedSiteDelimiter = true;
					break;

				default:
					if (multiResidueBlockMode > 0)
						multiResidueBlock.push_back(motif_[i]);
                    else
                    {
					    (parsedSiteDelimiter ? cFilters_ : nFilters_).push_back(IsValidAA());
                        pFilter = &(parsedSiteDelimiter ? cFilters_ : nFilters_).back();

                        if (motif_[i] == 'X' || motif_[i] == '.')
                            std::fill(pFilter->begin(), pFilter->end(), true);
                        else
                            (*pFilter)[motif_[i]] = true;
                    }
			}
		}

		if( multiResidueBlockMode > 0 )
            throw runtime_error("[Digestion::Motif::Impl::parseMotif()] Mismatched multi-residue block opening bracket in motif \"" + motif_ + "\"");
    }

    string motif_;

    // Let offset be the residue immediately N terminal to a potential digestion site
    // and offset+1 be the residue immediately C terminal.
    // nFilters_.rbegin() tests offset, rbegin()+1 tests offset-1, etc.
    // cFilters_.begin() tests offset+1, begin()+1 tests offset+1, etc.
    vector<IsValidAA> nFilters_;
    vector<IsValidAA> cFilters_;
};


PWIZ_API_DECL
Digestion::Motif::Motif(const string& motif)
:   impl_(new Impl(motif))
{
}

PWIZ_API_DECL
Digestion::Motif::Motif(const char* motif)
:   impl_(new Impl(motif))
{
}

PWIZ_API_DECL Digestion::Motif::Motif(const Motif& other)
:   impl_(new Impl(*other.impl_))
{
}

PWIZ_API_DECL Digestion::Motif& Digestion::Motif::operator=(const Motif& rhs)
{
    impl_.reset(new Impl(*rhs.impl_));
    return *this;
}

PWIZ_API_DECL Digestion::Motif::~Motif()
{
}

PWIZ_API_DECL bool Digestion::Motif::testSite(const string& sequence, int offset) const
{
    return impl_->testSite(sequence, offset);
}


namespace {

class CleavageAgentInfo : public boost::singleton<CleavageAgentInfo>
{
    public:
    CleavageAgentInfo(boost::restricted)
    {
        const vector<CVID>& cvids = pwiz::cv::cvids();
        for (vector<CVID>::const_iterator itr = cvids.begin();
             itr != cvids.end(); ++itr)
        {
            if (!cvIsA(*itr, MS_cleavage_agent_name))
                continue;

            const CVTermInfo& cvTermInfo = pwiz::cv::cvTermInfo(*itr);
            multimap<string, CVID>::const_iterator regexRelationItr =
                cvTermInfo.otherRelations.find("has_regexp");
            if (regexRelationItr != cvTermInfo.otherRelations.end())
            {
                cleavageAgents_.insert(*itr);
                cleavageAgentNames_.push_back(cvTermInfo.name);
                cleavageAgentNameToCvidMap_[bal::to_lower_copy(cvTermInfo.name)] = *itr;
                const CVTermInfo& cleavageAgentRegexTerm = pwiz::cv::cvTermInfo(regexRelationItr->second);
                cleavageAgentToRegexMap_[*itr] = &cleavageAgentRegexTerm;
            }
        }
    }

    const set<CVID>& cleavageAgents() const {return cleavageAgents_;}
    const vector<string>& cleavageAgentNames() const {return cleavageAgentNames_;}

    CVID getCleavageAgentByName(const string& agentName) const
    {
        string name = bal::to_lower_copy(agentName);
        map<string, CVID>::const_iterator agentTermItr = cleavageAgentNameToCvidMap_.find(name);

        if (agentTermItr == cleavageAgentNameToCvidMap_.end())
            return CVID_Unknown;

        return agentTermItr->second;
    }

    const std::string& getCleavageAgentRegex(CVID agentCvid) const
    {
        if (!pwiz::cv::cvIsA(agentCvid, MS_cleavage_agent_name))
            throw invalid_argument("[getRegexForCleavageAgent] CVID is not a cleavage agent.");

        map<CVID, const CVTermInfo*>::const_iterator regexTermItr =
            cleavageAgentToRegexMap_.find(agentCvid);

        if (regexTermItr == cleavageAgentToRegexMap_.end())
            throw runtime_error("[getRegexForCleavageAgent] No regex relation for cleavage agent " + cvTermInfo(agentCvid).name);

        return regexTermItr->second->name;
    }

    private:
    set<CVID> cleavageAgents_;
    vector<string> cleavageAgentNames_;
    map<string, CVID> cleavageAgentNameToCvidMap_;
    map<CVID, const CVTermInfo*> cleavageAgentToRegexMap_;
};

} // namespace


const set<CVID>& Digestion::getCleavageAgents()
{
    return CleavageAgentInfo::instance->cleavageAgents();
}

const vector<string>& Digestion::getCleavageAgentNames()
{
    return CleavageAgentInfo::instance->cleavageAgentNames();
}

CVID Digestion::getCleavageAgentByName(const string& agentName)
{
    return CleavageAgentInfo::instance->getCleavageAgentByName(agentName);
}

const string& Digestion::getCleavageAgentRegex(CVID agentCvid)
{
    return CleavageAgentInfo::instance->getCleavageAgentRegex(agentCvid);
}


class Digestion::Impl
{
    public:
    Impl(const Peptide& peptide, const std::vector<CVID>& cleavageAgents, const Config& config)
        :   peptide_(peptide), config_(config)
    {
        if (cleavageAgents.size() == 1)
        {
            cleavageAgentRegex_ = getCleavageAgentRegex(cleavageAgents[0]);
            return;
        }

        string mergedRegex = "((" + getCleavageAgentRegex(cleavageAgents[0]);
        for (size_t i=1; i < cleavageAgents.size(); ++i)
            mergedRegex += ")|(" + getCleavageAgentRegex(cleavageAgents[i]);
        mergedRegex += "))";

        cleavageAgentRegex_ = mergedRegex;
    }

    Impl(const Peptide& peptide, const boost::regex& cleavageAgentRegex, const Config& config)
        :   peptide_(peptide), config_(config), cleavageAgentRegex_(cleavageAgentRegex)
    {
    }

    Impl(const Peptide& peptide, const std::vector<ProteolyticEnzyme>& enzymes, const Config& config)
        :   peptide_(peptide), config_(config)
    {
        motifs_.push_back(Motif("{|"));
        motifs_.push_back(Motif("|}"));
        for (size_t i=0; i < enzymes.size(); ++i)
        {
            switch (enzymes[i])
            {
                case ProteolyticEnzyme_Trypsin:
                    motifs_.push_back(Motif("[KR]|"));
                    break;
                case ProteolyticEnzyme_Chymotrypsin:
                    motifs_.push_back(Motif("[FWY]|"));
                    break;
                case ProteolyticEnzyme_Clostripain:
                    motifs_.push_back(Motif("R|"));
                    break;
                case ProteolyticEnzyme_CyanogenBromide:
                    motifs_.push_back(Motif("M|[^ST]"));
                    break;
                case ProteolyticEnzyme_Pepsin:
                    motifs_.push_back(Motif("[FWY]|"));
                    break;
            }
        }
    }

    Impl(const Peptide& peptide, const vector<Motif>& motifs, const Config& config)
        :   peptide_(peptide), config_(config), motifs_(motifs)
    {
        motifs_.push_back(Motif("{|"));
        motifs_.push_back(Motif("|}"));
    }

    inline void digest() const
    {
        if (sites_.empty())
        {
            try
            {
                const string& sequence = peptide_.sequence();

                if (!cleavageAgentRegex_.empty())
                {
                    std::string::const_iterator start = sequence.begin();
                    std::string::const_iterator end = sequence.end();
                    boost::smatch what;
                    boost::match_flag_type flags = boost::match_default;
                    while (regex_search(start, end, what, cleavageAgentRegex_, flags))
                    {
                        sites_.push_back(int(what[0].first-sequence.begin()-1));

                        // update search position and flags
                        start = what[0].second;
                        flags |= boost::match_prev_avail;
                        flags |= boost::match_not_bob;
                    }

                    // if regex didn't match n-terminus, insert it
                    if (sites_.empty() || sites_.front() > -1)
                        sites_.insert(sites_.begin(), -1);

                    // if regex didn't match c-terminus, insert it
                    if (sites_.back() < (int)sequence.length()-1)
                        sites_.push_back(sequence.length()-1);
                }
                else
                {
                    // iterate to find all valid digestion sites
                    for (int offset = -1, end = (int) sequence.length(); offset < end; ++offset)
                    {
                        for (size_t i=0; i < motifs_.size(); ++i)
                            if (motifs_[i].testSite(sequence, offset))
                            {
                                sites_.push_back(offset);
                                break; // skip other motifs after finding a valid site
                            }
                    }
                }
            }
            catch (exception& e)
            {
                throw runtime_error(string("[Digestion::Impl::digest()] ") + e.what());
            }
        }
    }

    private:
    const Peptide& peptide_;
    Config config_;
    boost::regex cleavageAgentRegex_;
    vector<Motif> motifs_;
    friend class Digestion::const_iterator::Impl;

    // precalculated offsets to digestion sites in order of occurence;
    // the sites are between offset and offset+1;
    // -1 is the N terminus digestion site
    // peptide_.sequence().length()-1 is the C terminus digestion site
    mutable vector<int> sites_;
};


PWIZ_API_DECL
Digestion::Digestion(const Peptide& peptide,
                     CVID cleavageAgent,
                     const Config& config)
:   impl_(new Impl(peptide, vector<CVID>(1, cleavageAgent), config))
{
}

PWIZ_API_DECL
Digestion::Digestion(const Peptide& peptide,
                     const vector<CVID>& cleavageAgents,
                     const Config& config)
:   impl_(new Impl(peptide, cleavageAgents, config))
{
}

PWIZ_API_DECL
Digestion::Digestion(const Peptide& peptide,
                     const boost::regex& cleavageAgentRegex,
                     const Config& config)
:   impl_(new Impl(peptide, cleavageAgentRegex, config))
{
}


// DEPRECATED CONSTRUCTORS

PWIZ_API_DECL
Digestion::Digestion(const Peptide& peptide,
                     ProteolyticEnzyme enzyme,
                     const Config& config)
:   impl_(new Impl(peptide, vector<ProteolyticEnzyme>(1, enzyme), config))
{
}

PWIZ_API_DECL
Digestion::Digestion(const Peptide& peptide,
                     const vector<ProteolyticEnzyme>& enzymes,
                     const Config& config)
:   impl_(new Impl(peptide, enzymes, config))
{
}

PWIZ_API_DECL
Digestion::Digestion(const Peptide& peptide,
                     const Motif& motif,
                     const Config& config)
:   impl_(new Impl(peptide, vector<Motif>(1, motif), config))
{
}

PWIZ_API_DECL
Digestion::Digestion(const Peptide& peptide,
                     const vector<Motif>& motifs,
                     const Config& config)
:   impl_(new Impl(peptide, motifs, config))
{
}

PWIZ_API_DECL Digestion::~Digestion()
{
}

PWIZ_API_DECL Digestion::const_iterator Digestion::begin() const
{
    return const_iterator(*this);
}

PWIZ_API_DECL Digestion::const_iterator Digestion::end() const
{
    return const_iterator();
}


class Digestion::const_iterator::Impl
{
    public:
    Impl(const Digestion& digestion)
        :   digestionImpl_(*digestion.impl_),
            config_(digestionImpl_.config_),
            sequence_(digestionImpl_.peptide_.sequence()),
            sites_(digestionImpl_.sites_)
    {
        digestionImpl_.digest();
        try
        {
            switch (config_.minimumSpecificity)
            {
                default:
                case FullySpecific:
                    initFullySpecific();
                    break;

                case SemiSpecific: // TODO: optimize semi-specific
                case NonSpecific:
                    initNonSpecific();
                    break;
            }
        }
        catch (exception& e)
        {
            throw runtime_error(string("[Digestion::const_iterator::Impl::Impl()] ") + e.what());
        }
    }

    inline void initFullySpecific()
    {
        begin_ = end_ = sites_.end();

        // iteration requires at least 2 digestion sites
        if (sites_.size() < 2)
            return;

        // try each possible pair of digestion sites;
        // initialize begin_ and end_ to the first pair that meet
        // config's filtering criteria
        for (size_t i=0; i < sites_.size(); ++i)
        {
            int testBegin = sites_[i];

            for (size_t j=1; j < sites_.size(); ++j)
            {
                int testEnd = sites_[j];

                int curMissedCleavages = int(end_ - begin_)-1;
                if (curMissedCleavages > config_.maximumMissedCleavages)
                    break;

                int curLength = testEnd - testBegin;
                if (curLength > config_.maximumLength)
                    break;
                if (curLength < config_.minimumLength)
                    continue;

                begin_ = sites_.begin()+i;
                end_ = sites_.begin()+j;
                break;
            }

            if (begin_ != sites_.end())
                break;
        }
    }

    inline void initNonSpecific()
    {
        begin_ = end_ = sites_.begin();
        beginNonSpecific_ = endNonSpecific_ = sequence_.length();
        int maxLength = (int) sequence_.length();

        // try each possible pair of digestion sites;
        // initialize beginNonSpecific_ and beginNonSpecific_ to the first pair that meet
        // config's filtering criteria
        bool passesFilter = false;
        for (int testBegin = -1; testBegin < maxLength && !passesFilter; ++testBegin)
        {
            for (int testEnd = testBegin+config_.minimumLength; testEnd < maxLength; ++testEnd)
            {
                // end offset is too far, start again with a new begin offset
                int curLength = testEnd - testBegin;
                if (curLength > config_.maximumLength)
                    break;

                beginNonSpecific_ = testBegin;
                endNonSpecific_ = testEnd;
                while (begin_ != sites_.end() && *begin_ <= beginNonSpecific_)
                    ++begin_;
                if (begin_ != sites_.end())
                {
                    end_ = begin_--;
                    while (end_ != sites_.end() && *end_ < endNonSpecific_)
                        ++end_;
                }

                // end offset is too far, start again with a new begin offset
                int curMissedCleavages = int(end_ - begin_)-1;
                if (curMissedCleavages > config_.maximumMissedCleavages)
                    break;

                if (beginNonSpecific_ != maxLength)
                {
                    passesFilter = true;
                    break;
                }
            }
        }
    }

    const DigestedPeptide& peptide() const
    {
        try
        {
	  string prefix = "";
	  string suffix = "";
            if (!peptide_.get())
            {
	      switch (config_.minimumSpecificity)
                {
                    default:
                    case FullySpecific:
		      if((*begin_ >= 0) && (*begin_ < sequence_.length())){
			prefix = sequence_.substr(*begin_,1); //this could be changed to be something other than 1 by a config option later
		      }
		      if(*end_ != sequence_.length()){
			suffix = sequence_.substr(*end_+1,1);
		      }
		      peptide_.reset(
                            new DigestedPeptide(sequence_.begin()+(*begin_+1),
                                                sequence_.begin()+(*end_+1),
                                                *begin_+1,
                                                int(end_ - begin_)-1,
                                                true,
                                                true,
						prefix, 
						suffix
						));
			                    
			break;

                    case SemiSpecific:
                    case NonSpecific:
		      if((beginNonSpecific_ >= 0) && (beginNonSpecific_ < sequence_.length())){
			prefix = sequence_.substr(beginNonSpecific_,1); //this could be changed to be something other than 1 by a config option later
		      }
		      if(endNonSpecific_ != sequence_.length()){
			suffix = sequence_.substr(endNonSpecific_+1,1);
		      }
                        peptide_.reset(
                            new DigestedPeptide(sequence_.begin()+(beginNonSpecific_+1),
                                                sequence_.begin()+(endNonSpecific_+1),
                                                beginNonSpecific_+1,
                                                int(end_ - begin_)-1,
                                                begin_ != sites_.end() && *begin_ == beginNonSpecific_,
                                                end_ != sites_.end() && *end_ == endNonSpecific_,
						prefix,
						suffix
						));
                        break;
                }
            }
            return *peptide_;
        }
        catch (exception& e)
        {
            throw runtime_error(string("[Digestion::const_iterator::Impl::peptide()] ") + e.what());
        }
    }

    inline void nextFullySpecific()
    {
        bool newBegin = (end_ == sites_.end());
        if (!newBegin)
        {
            // advance end_ to the next digestion site
            bool foundNextValidSitePair = false;
            for (++end_; end_ != sites_.end(); ++end_)
            {
                int curMissedCleavages = int(end_ - begin_)-1;
                if (curMissedCleavages > config_.maximumMissedCleavages)
                    break;

                int curLength = *end_ - *begin_;
                if (curLength > config_.maximumLength)
                    break;
                if (curLength < config_.minimumLength)
                    continue;

                foundNextValidSitePair = true;
                break;
            }

            // there may not be another site that makes a valid site pair
            newBegin = !foundNextValidSitePair;
        }

        if (newBegin)
        {
            // advance to the next valid digestion site pair
            bool foundNextValidSitePair = false;
            for (++begin_; begin_ != sites_.end(); ++begin_)
            {
                for (end_ = begin_+1; end_ != sites_.end(); ++end_)
                {
                    int curMissedCleavages = int(end_ - begin_)-1;
                    if (curMissedCleavages > config_.maximumMissedCleavages)
                        break;

                    int curLength = *end_ - *begin_;
                    if (curLength > config_.maximumLength)
                        break;
                    if (curLength < config_.minimumLength)
                        continue;

                    foundNextValidSitePair = true;
                    break;
                }

                if (foundNextValidSitePair)
                    break;
            }
        }
    }

    inline void nextNonSpecific()
    {
        int maxLength = (int) sequence_.length();
        bool newBegin = (endNonSpecific_ == maxLength);
        if (!newBegin)
        {
            // advance endNonSpecific_ to the next digestion site
            bool foundNextValidSitePair = false;
            end_ = begin_;
            for (++endNonSpecific_; endNonSpecific_ < maxLength; ++endNonSpecific_)
            {
                while (end_ != sites_.end() && *end_ < endNonSpecific_)
                    ++end_;

                int curMissedCleavages = int(end_ - begin_)-1;
                if (curMissedCleavages > config_.maximumMissedCleavages)
                    break;

                int curLength = endNonSpecific_ - beginNonSpecific_;
                if (curLength > config_.maximumLength)
                    break;
                if (curLength < config_.minimumLength)
                    continue;

                foundNextValidSitePair = true;
                break;
            }

            // there may not be another site that makes a valid site pair
            newBegin = !foundNextValidSitePair;
        }

        if (newBegin)
        {
            // advance to the next valid digestion site pair
            bool foundNextValidSitePair = false;
            for (++beginNonSpecific_; beginNonSpecific_ < maxLength; ++beginNonSpecific_)
            {
                while (begin_ != sites_.end() && *begin_ <= beginNonSpecific_)
                    ++begin_;
                end_ = begin_--;

                for (endNonSpecific_ = beginNonSpecific_+config_.minimumLength; endNonSpecific_ < maxLength; ++endNonSpecific_)
                {
                    while (end_ != sites_.end() && *end_ < endNonSpecific_)
                        ++end_;

                    int curMissedCleavages = int(end_ - begin_)-1;
                    if (curMissedCleavages > config_.maximumMissedCleavages)
                        break;

                    int curLength = endNonSpecific_ - beginNonSpecific_;
                    if (curLength > config_.maximumLength)
                        break;
                    if (curLength < config_.minimumLength)
                        continue;

                    foundNextValidSitePair = true;
                    break;
                }

                if (foundNextValidSitePair)
                    break;
            }
        }
    }

    inline Impl& operator++()
    {
        try
        {
            peptide_.reset();

            switch (config_.minimumSpecificity)
            {
                default:
                case FullySpecific:
                    nextFullySpecific();
                    break;

                case SemiSpecific:
                    while (beginNonSpecific_ < (int) sequence_.length())
                    {
                        nextNonSpecific();
                        if ((begin_ != sites_.end() && *begin_ == beginNonSpecific_) ||
                            (end_ != sites_.end() && *end_ == endNonSpecific_))
                            break;
                    }
                    break;

                case NonSpecific:
                    nextNonSpecific();
                    break;
            }
        }
        catch (exception& e)
        {
            throw runtime_error(string("[Digestion::Impl::operator++()] ") + e.what());
        }

        return *this;
    }

    inline Impl operator++(int)
    {
        Impl tmp(*this);
        ++*this;
        return tmp;
    }

    inline bool atEnd()
    {
        switch (config_.minimumSpecificity)
        {
            case SemiSpecific: // TODO: optimize semi-specific
            case NonSpecific:
                return beginNonSpecific_ == (int) sequence_.length();

            case FullySpecific:
            default:
                return begin_ == sites_.end();
        }
    }

    private:
    const Digestion::Impl& digestionImpl_;
    const Config& config_;
    const string& sequence_;
    const vector<int>& sites_;

    // used for all digests
    // fully specific: iterator to the current peptide's N terminal offset-1
    // semi- and non-specific: iterator to the previous valid digestion site before or at beginNonSpecific_
    vector<int>::const_iterator begin_;

    // fully specific: iterator to the current peptide's C terminal offset
    // semi- and non-specific: iterator to the next valid digestion site at or after endNonSpecific_
    vector<int>::const_iterator end_;

    // used for semi- and non-specific digest
    int beginNonSpecific_;
    int endNonSpecific_;

    mutable shared_ptr<DigestedPeptide> peptide_;
    friend class Digestion::const_iterator;
};

PWIZ_API_DECL Digestion::const_iterator::const_iterator()
{
}

PWIZ_API_DECL Digestion::const_iterator::const_iterator(const Digestion& digestion)
:   impl_(new Impl(digestion))
{
}

PWIZ_API_DECL Digestion::const_iterator::const_iterator(const const_iterator& rhs)
:   impl_(rhs.impl_.get() ? new Impl(*rhs.impl_) : 0)
{
}

PWIZ_API_DECL Digestion::const_iterator::~const_iterator()
{
}

PWIZ_API_DECL const DigestedPeptide& Digestion::const_iterator::operator*() const
{
    return impl_->peptide();
}

PWIZ_API_DECL const DigestedPeptide* Digestion::const_iterator::operator->() const
{
    return &(impl_->peptide());
}

PWIZ_API_DECL Digestion::const_iterator& Digestion::const_iterator::operator++()
{
    ++(*impl_);
    return *this;
}

PWIZ_API_DECL Digestion::const_iterator Digestion::const_iterator::operator++(int)
{
    const_iterator tmp(*this);
    ++(*impl_);
    return tmp;
}

PWIZ_API_DECL bool Digestion::const_iterator::operator!=(const Digestion::const_iterator& that) const
{
    return !(*this == that);
}

PWIZ_API_DECL bool Digestion::const_iterator::operator==(const Digestion::const_iterator& that) const
{
    bool gotThis = this->impl_.get() != NULL;
    bool gotThat = that.impl_.get() != NULL;

    if (gotThis && gotThat)
        return this->impl_->begin_ == that.impl_->begin_ &&
               this->impl_->end_ == that.impl_->end_;
    else if (!gotThis && !gotThat) // end() == end()
        return true;
    else if (gotThis)
        return this->impl_->atEnd();
    else // gotThat
        return that.impl_->atEnd();
}

} // namespace proteome
} // namespace pwiz
