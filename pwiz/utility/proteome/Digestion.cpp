//
// Digestion.cpp 
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
#include "utility/misc/CharIndexedVector.hpp"
#include "utility/misc/Exception.hpp"

namespace pwiz {
namespace proteome {


using namespace std;
using namespace pwiz::util;

#undef SECURE_SCL


PWIZ_API_DECL
Digestion::Config::Config(int maximumMissedCleavages,
                          //double minimumMass,
                          //double maximumMass,
                          int minimumLength,
                          int maximumLength)
:   maximumMissedCleavages(maximumMissedCleavages),
    //minimumMass(minimumMass), maximumMass(maximumMass),
    minimumLength(minimumLength), maximumLength(maximumLength)
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
        AminoAcid::Info info;
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


class Digestion::Impl
{
    public:
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
            }
        }
    }

    Impl(const Peptide& peptide, const vector<Motif>& motifs, const Config& config)
        :   peptide_(peptide), motifs_(motifs), config_(config)
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

                // iterate to find all valid digestion sites
                for (int offset = -1, end = (int) sequence.length(); offset < end; ++offset)
                {
                    for (size_t i=0; i < motifs_.size(); ++i)
                    {
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
    vector<Motif> motifs_;
    Config config_;
    friend class Digestion::const_iterator::Impl;

    // precalculated offsets to digestion sites in order of occurence;
    // the sites are between offset and offset+1;
    // -1 is the N terminus digestion site
    // peptide_.sequence().length()-1 is the C terminus digestion site
    mutable vector<int> sites_;
};

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
        catch (exception& e)
        {
            throw runtime_error(string("[Digestion::const_iterator::Impl::Impl()] ") + e.what());
        }
    }

    const Peptide& peptide() const
    {
        try
        {
            if (!peptide_.get())
            {
                peptide_.reset(new Peptide(sequence_.begin()+(*begin_+1), sequence_.begin()+(*end_+1)));
            }
            return *peptide_;
        }
        catch (exception& e)
        {
            throw runtime_error(string("[Digestion::const_iterator::Impl::peptide()] ") + e.what());
        }
    }

    inline Impl& operator++()
    {
        try
        {
            peptide_.release();

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

    private:
    const Digestion::Impl& digestionImpl_;
    const Config& config_;
    const string& sequence_;
    const vector<int>& sites_;
    vector<int>::const_iterator begin_; // iterator to the current peptide's N terminal offset-1
    vector<int>::const_iterator end_; // iterator to the current peptide's C terminal offset
    mutable auto_ptr<Peptide> peptide_;
    friend class Digestion::const_iterator;
};

PWIZ_API_DECL Digestion::const_iterator::const_iterator()
:   impl_(0)
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

PWIZ_API_DECL const Peptide& Digestion::const_iterator::operator*() const
{
    return impl_->peptide();
}

PWIZ_API_DECL const Peptide* Digestion::const_iterator::operator->() const
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
        return this->impl_->begin_ == this->impl_->sites_.end();
    else // gotThat
        return that.impl_->begin_ == that.impl_->sites_.end();
}

} // namespace proteome
} // namespace pwiz
