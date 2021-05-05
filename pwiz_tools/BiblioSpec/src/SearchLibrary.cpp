//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

//class definition of Search Library

#include "SearchLibrary.h"
#include "BlibUtils.h"

namespace BiblioSpec {

// no default constructor so that SearchLibrary always is initialized with
// correct options
SearchLibrary::SearchLibrary(vector<string>& libfilenames,
                             const ops::variables_map& options_table) :
  peakProcessor_(options_table), 
  weibullEstimator_(options_table),
  mzWindow_(options_table["mz-window"].as<double>()),
  minPeaks_(options_table["min-peaks"].as<int>()),
  minSpecCharge_(options_table["low-charge"].as<int>()),
  maxSpecCharge_(options_table["high-charge"].as<int>()),
  compute_pvalues_(options_table["compute-p-values"].as<bool>()),
  minWeibullScores_(options_table["min-weibull-scores"].as<int>()),
  decoysPerTarget_(options_table["decoys-per-target"].as<int>()),
  decoyMzShift_(options_table["circ-shift"].as<double>()),
  shiftRawSpectra_(options_table["shift-raw-spectrum"].as<bool>()),
  querySorted_(options_table.count("mz-sort") != 0),
  printAll_(options_table["print-all-params"].as<bool>())
{

    // create a list of LibReaders from the filenames
    for(size_t i = 0; i < libfilenames.size(); i++){
        Verbosity::debug("Creating reader for library %s.", 
                         libfilenames.at(i).c_str());
        libraries_.push_back(new LibReader(libfilenames.at(i).c_str()));
    }
  
    // open file for printing weibull parameters, if requested
    // throws exception if no value, so check first
    if( options_table.count("weibull-param-file") ){
        string filename = options_table["weibull-param-file"].as<string>();
        weibullParamFile_.open(filename.c_str());
        if( ! weibullParamFile_.is_open() ){
            Verbosity::error("Could not open output file, '%s', "
                             "for Weibull parameters.", filename.c_str());
        }
        // print header
        weibullParamFile_ << "scan\teta\tbeta\tshift\tcorr\tnum-scores-used" 
                          << endl;
        weibullParamFile_.precision(4);
    }
} 

SearchLibrary::~SearchLibrary()
{
    for(size_t i = 0; i < cachedDecoySpectra_.size(); i++){
        delete cachedDecoySpectra_.at(i);
        cachedDecoySpectra_.at(i) = NULL;
    }
    for(size_t i = 0; i < cachedSpectra_.size(); i++){
        delete cachedSpectra_.at(i);
        cachedSpectra_.at(i) = NULL;
    }
    for(size_t i = 0; i < libraries_.size(); i++){
        delete libraries_.at(i);
        libraries_.at(i) = NULL;
    }
    if( weibullParamFile_.is_open() ){
        weibullParamFile_.close();
    }
}

bool floatDescending(float a, float b){
    return a > b;
}

/**
 * Find all spectra in the library(s) in the appropriate m/z range,
 * process query spectrum, and compare given query to all library spectra.
 */
void SearchLibrary::searchSpectrum(BiblioSpec::Spectrum& querySpec){

    Verbosity::debug("Searching spectrum %i", querySpec.getScanNumber());

    // process query spectrum
    peakProcessor_.processPeaks(&querySpec);
    if( querySpec.getNumProcessedPeaks() < minPeaks_ ){
        Verbosity::warn("Spectrum %i has %i peaks, fewer than the minimum.",
                        querySpec.getScanNumber(), 
                        querySpec.getNumProcessedPeaks());
        return;
    }
    
    // clear out previous results and get new lib spec
    updateSpectrumCache(querySpec.getMz());
    targetMatches_.clear();
    decoyMatches_.clear();

    if(cachedSpectra_.size() == 0 ){
        Verbosity::warn("No library spectra found for query %d "
                        "(precursor m/z %.2f).", querySpec.getScanNumber(), 
                        querySpec.getMz());
        return;
    }

    runSearch(querySpec);
}

/**
 * Update the contents of the spectrum cache for the next query
 * spectrum.  If query are NOT sorted, empties cache and fetches all
 * spectra in search window.  If query are sorted, removes spectra
 * with mz lower than current search window and adds  
 * spectra up to the max mz of the search window.  Add spec of all
 * charge states and do the charge state filtering at the spectrum
 * comparison. 
 */
void SearchLibrary::updateSpectrumCache(double queryMz){

    // mz range for the current query spectrum
    double searchMinMz = queryMz - mzWindow_;
    double searchMaxMz = queryMz + mzWindow_;

    // if query are not sorted, empty cache
    if( ! querySorted_ ){
        clearDeque(cachedSpectra_); 
        clearDeque(cachedDecoySpectra_); 
    }

    // remove low mz values from cache
    //cerr << "Query has mz " << queryMz << ". Removing mz's less than " << searchMinMz << endl;
    deque<RefSpectrum*>::iterator it = cachedSpectra_.begin();
    while( it != cachedSpectra_.end() && (*it)->getMz() < searchMinMz ){
        //cerr << "Deleteing mz " << (*it)->getMz() << endl;
        it++;
        cachedSpectra_.pop_front(); 
    }
    it = cachedDecoySpectra_.begin();
    while( it != cachedDecoySpectra_.end() && (*it)->getMz() < searchMinMz ){
        it++;
        cachedDecoySpectra_.pop_front(); 
    }

    // find the lower bound of new spec to get, min search range if cache empty
    double addMinMz = searchMinMz;
    if( ! cachedSpectra_.empty() ){ 
        addMinMz = cachedSpectra_.back()->getMz(); 
    }

    // get spec from all libs
    getLibrarySpec(addMinMz, searchMaxMz);

    // sort the cache
    sort(cachedSpectra_.begin(), cachedSpectra_.end(), compSpecPtrMz());
    sort(cachedDecoySpectra_.begin(), cachedDecoySpectra_.end(), 
         compSpecPtrMz());
}

/**
 * Before searching each spectrum, set the appropriate precursor m/z
 * range and charge states.
 */
void SearchLibrary::initLibraries(Spectrum& querySpec){
    // TODO: for cache, set min as max(cacheMax, searchMin)
    //       max is still searchMax. charge is all charges
    // set the precursor m/z range  and charge states in each library
    double minMZ = querySpec.getMz() - mzWindow_;
    double maxMZ = querySpec.getMz() + mzWindow_;
    for(size_t i = 0; i < libraries_.size(); i++){
        LibReader* curLibrary = libraries_.at(i);

        curLibrary->setLowMZ(minMZ);
        curLibrary->setHighMZ(maxMZ);

        // set min and max charges to look for either by the query
        // charge or by the given parameters
        /* if( ignoreQueryCharge ){ ...use current else clause } else if*/
        //if( querySpec.sizeZ() == 1 ){
        const vector<int>& charges = querySpec.getPossibleCharges();
        if( charges.size() == 1 ){
            curLibrary->setCharge(charges.front());
        } else { // TODO instead use min and max charge of query as range
            curLibrary->setLowChg(minSpecCharge_);
            curLibrary->setHighChg(maxSpecCharge_);
            curLibrary->setCharge(-1); // hack to say there is no one
                                       // charge
        } /* else {

            int min = 100;
            int max = 0;
            for(i=0; i < querySpec.sizeZ(); i++){
            if( querySpec.atZ(i).z > max )
                max = querySpec.atZ(i).z; 
            }
            if( querySpec.atZ(i).z < min )
                min = querySpec.atZ(i).z; 
            }
            curLibrary->setLowChg(min);
            curLibrary->setHighChg(max);
        } */
    }
}

/**
 * Fill cachedSpectra_ with reference spectra from the libraries.
 * Also fills cachedDecoySpectra_ if decoysPerTarget_ is non-zero.
 * Spectra will have precursor m/z between minMz and maxMz and be at
 * all charge states.
 * Generates randomized spectra if shiftMz is greater than 0.  Can
 * either process peaks and then shift or shift then process.
 */
void SearchLibrary::getLibrarySpec(double minMz, double maxMz){
    
    // for each library being searched
    for(size_t lib_i = 0; lib_i < libraries_.size(); lib_i++){
        // library index is 0 for decoy spectra
        int libIndex = lib_i + 1;

        // after adding, preprocess starting with this index
        size_t startIdx = cachedSpectra_.size(); 
        // TODO add a min-peaks optin and use here for 5
        libraries_.at(lib_i)->getSpecInMzRange(minMz, maxMz, 5, cachedSpectra_);
        Verbosity::comment(V_DETAIL, "Found %d spec between %.2f and %.2f.",
                           cachedSpectra_.size() - startIdx, minMz, maxMz);

        // process each spectrum and set the lib id
        for(size_t spec_i = startIdx; spec_i < cachedSpectra_.size(); spec_i++){
            RefSpectrum* curSpec = cachedSpectra_.at(spec_i);
            curSpec->setLibID(libIndex); 
            peakProcessor_.processPeaks(curSpec);
        }

        // generate decoys
        if( decoysPerTarget_ > 0 ){
            Verbosity::debug("Generating decoy spectra.");
            generateDecoySpectra(startIdx);
            if( shiftRawSpectra_ ){ // decoys haven't been processed
                for(size_t spec_i = startIdx; 
                    spec_i < cachedDecoySpectra_.size(); 
                    spec_i++){
                    peakProcessor_.processPeaks(cachedDecoySpectra_.at(spec_i));
                }
            }
        }
    } // next library
    
}

/**
 * Fill the cachedDecoySpectra with shifted copies of those in
 * cachedSpectra_.  Copies all spectra from startIndex to end.
 */
void SearchLibrary::generateDecoySpectra(int startIndex){
    double shiftMz = decoyMzShift_;
    for(int i = 0; i < decoysPerTarget_; i++){
        for(int spec_i=startIndex; spec_i<(int)cachedSpectra_.size(); spec_i++){
            RefSpectrum* decoy = 
                cachedSpectra_.at(spec_i)->newDecoy(shiftMz, 
                                                    shiftRawSpectra_);
            if( decoy ){ // only add if we could make a decoy from this target
                cachedDecoySpectra_.push_back(decoy);
            }
        }

        shiftMz += decoyMzShift_;
    } // next set of decoys
}

/**
 * Compare the given query spectrum to all library spectra.  Create a
 * match for each and add to matches.
 */
void SearchLibrary::scoreMatches(Spectrum& s, deque<RefSpectrum*>& spectra,
                                 vector<Match>& matches ){
    Verbosity::debug("Scoring %d matches.", spectra.size());
    // get the charge states we will search
    const vector<int>& charges = s.getPossibleCharges();
    
    // compare all ref spec to query, create match for each
    for(size_t i=0; i< spectra.size(); i++) {
        // is there a better place to check this?
        if(spectra.at(i)->getNumProcessedPeaks() == 0 ){ 
            Verbosity::debug("Skipping library spectrum %d.  No peaks.", 
                             spectra.at(i)->getLibSpecID());
            continue;
        }
        
        if( ! checkCharge(charges, spectra.at(i)->getCharge()) ){
            continue;
        }
        
        Match thisMatch(&s, spectra.at(i));  
        
        thisMatch.setMatchLibID(spectra.at(i)->getLibID());
        
        Verbosity::comment(V_ALL, "Comparing query spec %d and library spec %d",
                           s.getScanNumber(), spectra.at(i)->getLibSpecID());
        
        DotProduct::compare(thisMatch); //static method
        
        // save match for reporting
        matches.push_back(thisMatch);
    }  
}
    
// assumes at least one spectrum in allRefs
void SearchLibrary::runSearch(Spectrum& s)
{
    scoreMatches(s, cachedSpectra_, targetMatches_);
    scoreMatches(s, cachedDecoySpectra_, decoyMatches_);

    // keep scores from all target psms for estimating Weibull parameters
    vector<double> allScores;
    if(compute_pvalues_){
        for(size_t i=0; i < targetMatches_.size(); i++){
            double dotp = targetMatches_[i].getScore(DOTP);
            allScores.push_back(dotp);
        }
    }

    // there may have been spectra in cachedSpectra_ but none at the
    // correct charge state.  Check again
    if( targetMatches_.size() == 0 ){
        Verbosity::warn("No library spectra found for query %d "
                        "(precursor m/z %.2f).", s.getScanNumber(), s.getMz());
        return;
    }
    if( compute_pvalues_ ){
        addNullScores(s, allScores);
    }

    // sort the matches descending
    sort(targetMatches_.begin(), targetMatches_.end(), compMatchDotScore); 
    sort(decoyMatches_.begin(), decoyMatches_.end(), compMatchDotScore); 

    setRank();
    
    if( printAll_ ){
        cout << "spec " << s.getScanNumber() << endl;
    }

    if( compute_pvalues_ ){
        weibullEstimator_.estimateParams(allScores);
        
        // print params to file
        if( weibullParamFile_.is_open() ){
            weibullParamFile_ << s.getScanNumber() << "\t"
                              << weibullEstimator_.getEta() << "\t"
                              << weibullEstimator_.getBeta() << "\t"
                              << weibullEstimator_.getShift() << "\t"
                              << weibullEstimator_.getCorrelation() << "\t"
                              << weibullEstimator_.getNumPointsFit() 
                //(int)(allScores.size() * fraction_to_fit_)
                              << endl;
        }
        setMatchesPvalues(allScores.size());
    }
}

void rank(vector<Match>& matches){
    if( matches.empty() ) return;

    double curScore = matches.front().getScore(DOTP);
    int curRank = 1;
    for(int i=0; i<(int)matches.size();i++) {
        if( matches.at(i).getScore(DOTP) != curScore ){
            curRank++;
            curScore = matches.at(i).getScore(DOTP);
        }
        matches.at(i).setRank(curRank);
    }

}

void SearchLibrary::setRank(){
    rank(targetMatches_);
    rank(decoyMatches_);
}


/**
 * Update each Match with its p_value.  Assumes parameters have been
 * estimated and that matches are sorted in descending order by score/p-value.
 */
void SearchLibrary::setMatchesPvalues(int numScoresForEstimation)
{
    for(int i=0; i<(int)targetMatches_.size();i++) {

        double dotp = targetMatches_.at(i).getScore(DOTP);
        double pval = weibullEstimator_.computePvalue(dotp);
        double correctedPval = 
            weibullEstimator_.bonferroniCorrectPvalue(pval);
        
        targetMatches_.at(i).setScore(RAW_PVAL, pval);
        targetMatches_.at(i).setScore(BONF_PVAL, correctedPval);
    }
}

/**
 * Return the target matches from the search
 */
const vector<Match>& SearchLibrary::getTargetMatches()
{
    return targetMatches_;
}

/**
 * Return the decoy matches from the search
 */
const vector<Match>& SearchLibrary::getDecoyMatches()
{
    return decoyMatches_;
}


// get weibull parameters
float SearchLibrary::getShape()
{
    return (float)weibullEstimator_.getBeta();
}

float SearchLibrary::getScale()
{
    return (float)weibullEstimator_.getEta();
}

float SearchLibrary::getFraction2Fit()
{
    return (float)weibullEstimator_.getFractionFit();
}
//get weibullHistogram
void SearchLibrary::getWeibullHistogram(int hist[], int numElements)
{
  
    for(int i=0; i<numElements; i++)
        hist[i] = 0;

    for(int i=1; i<(int)targetMatches_.size();i++) {
        int idx=(int)((targetMatches_.at(i)).getScore(DOTP)*100+0.5);
        hist[idx]++;
    }
    
}

bool SearchLibrary::compMatchDotScore(Match m1, Match m2)
{
    if (m1.getScore(DOTP) > m2.getScore(DOTP)){
        return true;
    } else if (m1.getScore(DOTP) == m2.getScore(DOTP)){
        return (m1.getRefSpec()->getLibSpecID() > m2.getRefSpec()->getLibSpecID() );
    }
    // else
    return false;
}

/* NOTE: Even though the p-values are not currently accurate, we will
   eventually want a mechanism for adding scores up to a minimum
   number.  Easier to adapt what we had than to scrap it for now and
   re-build from scratch later. */
/**
 *  Generate more scores by creating decoy spectra and comparing them
 *  to query.  Create one decoy for each target until there are the
 *  minimum number of scores.  Do not save decoy spectrum or its
 *  Match.  Makes no changes to cachedSpectra_.
 */
void SearchLibrary::addNullScores(Spectrum s, vector<double>& allScores){
    int shiftAmount = 5;

    while((int)allScores.size() < minWeibullScores_) {
        int specAdded = 0; // make sure spectra were successfully added
        
        //loop through all candidate refs, create shifted spectrum, compare
        for(size_t i=0; i < targetMatches_.size(); i++) {
            const RefSpectrum* targetSpec = targetMatches_.at(i).getRefSpec();
            RefSpectrum* decoySpec = targetSpec->newDecoy(shiftAmount,
                                                          shiftRawSpectra_);
            if( decoySpec == NULL ){
                continue;
            }
            specAdded++;
            Match thisMatch(&s, decoySpec); // spectrum deleted with match?
            thisMatch.setMatchLibID(0);
            Verbosity::comment(V_ALL, 
                      "Comparing query spec %d and shifted library spec %d",
                      s.getScanNumber(), decoySpec->getLibSpecID() );
            
            DotProduct::compare(thisMatch);
            allScores.push_back(thisMatch.getScore(DOTP));
            
        } // next ref spectrum

        shiftAmount += 5;

        if( specAdded == 0 ){
            break;  // avoid infinite loop if number of scores not increasing
        }
    } // next pass through all ref spectra
}

bool SearchLibrary::checkCharge(const vector<int>& queryCharges, int libCharge){

    // if no charges for the query spectrum, don't filter library spec by charge
    if( queryCharges.empty() ){ 
        return true;
    }

    for(size_t i = 0; i < queryCharges.size(); i++){
        if( libCharge == queryCharges[i] ){
            return true;
        }
    }
    // else we didn't find a matching charge state

    return false;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
