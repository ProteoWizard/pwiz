//
// $Id$
//
//
// Original author: William French <william.r.french <a.t> vanderbilt.edu>
//
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

#include "CwtPeakDetector.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/sort_together.hpp"


namespace {
    // Predicate for sorting vectors of ridgeLines 
    bool sortFinalCol(ridgeLine i, ridgeLine j) { return (i.Col < j.Col); }

    // Helper functions used by detect. The client does not need to see these.
    void ricker2d(const std::vector <double> &, const int, const int, const int, const double, const double, const double, std::vector <double> &);
    int getColLowBound(const std::vector <double> &, const double);
    int    getColHighBound(const std::vector <double> &, const double);
    double scoreAtPercentile(const double, const std::vector <double> &, const int);
    double convertColToMZ(const std::vector <double> &, const int);
}

namespace pwiz {
namespace analysis {

PWIZ_API_DECL
CwtPeakDetector::CwtPeakDetector(double minSnr, int fixedPeaksKeep, double mzTol, bool centroid)
: minSnr_(minSnr), fixedPeaksKeep_(fixedPeaksKeep), mzTol_(mzTol), centroid_(centroid)
{
    nScales = 10;

    // The scaling values relate to the wavelet scales to sample. Since the m/z spacing gradually increases with
    // increasing m/z values, I use a scaling for choosing which wavelet scales to sample, rather than
    // using fixed wavelet scales throughout the entire spectrum. The initialWidthScaling should not be
    // changed, 1.0 should be a good value. This is important as it defines the noise threshold in wavelet space.
    // The finalWidthScaling can be tuned, this should relate to the number of points in a peak (assuming equidistant
    // spacing between m/z points within a peak). If you expect roughly 12 points per peak, finalWidthScaling = 6 
    // should be a reasonable value.
    double initialWidthScaling = 1.0;
    double finalWidthScaling = 7.0; 
    double incrementScaling = (finalWidthScaling-initialWidthScaling) / double(nScales-1);
    scalings.resize(nScales);
    for (int i = 0; i<nScales ; i++)
        scalings[i] = initialWidthScaling + double(i) * incrementScaling;
}


PWIZ_API_DECL
void CwtPeakDetector::detect(const vector<double>& x_, const vector<double>& y_,
                                vector<double>& xPeakValues, vector<double>& yPeakValues,
                                vector<Peak>* peaks)
{

    if (x_.size() != y_.size())
        throw runtime_error("[CwtPeakDetector::detect()] x and y arrays must be the same size");

    //cout << "peakPicking on " << x.size() << " points" << endl;

    if (x_.size() <= 2) return;

    // local array copies
    vector<double> x(x_), y(y_);

    // ensure data is m/z sorted
    pwiz::util::sort_together(x, y);

    vector<double> binnedX; binnedX.reserve(x.size());
    vector<double> binnedY; binnedY.reserve(y.size());

    // bin identical m/z values
    if (x.size() > 1)
    {
        binnedX.push_back(x[0]);
        binnedY.push_back(y[0]);
        for (size_t i = 1; i < x.size(); ++i)
        {
            if (fabs(binnedX.back() - x[i]) < 1e-6)
            {
                for (; i < x.size() && fabs(binnedX.back() - x[i]) < 1e-6; ++i)
                    binnedY.back() += y[i];
                --i;
            }
            else
            {
                binnedX.push_back(x[i]);
                binnedY.push_back(y[i]);
            }
        }

        swap(x, binnedX);
        swap(y, binnedY);
    }

    int mzLength = x.size(); // number of data points in spectrum
    if (mzLength <= 2) return;
    int corrMatrixLength = 2 * mzLength - 1; // number of data points in a row of the correlation matrix

    // Data arrays
    vector < vector<double> > corrMatrix(nScales, vector<double>(corrMatrixLength,0.0)); // correlation matrix
    vector < vector<bool> > locMaxs(nScales, vector<bool>(corrMatrixLength,false)); // local maxima matrix
    vector <double> widths(mzLength,0.0);
    vector < vector< vector<int> > > waveletPoints(2, vector<vector<int> >(nScales,vector<int>(mzLength,0)));
    
    getScales( x, y, waveletPoints, widths );

    calcCorrelation( x, y, waveletPoints, widths, corrMatrix ); // calculate the correlation matrix

    // step 1: find maxima in each column
    // step 2: apply sliding window with fixed width to generate list of (row,col) maxima (i.e., "lines")
    // step 3: filter the list of maxima with SNR
    vector<ridgeLine> allLines; // identified/filtered lines
    vector <double> snrs; // snr associated with each line
    getPeakLines( corrMatrix, x, allLines, snrs );

    // refine the peak positions and remove peaks using fixedPeaksKeep_, if needed. 
    xPeakValues.reserve(allLines.size()), yPeakValues.reserve(allLines.size());
    refinePeaks( x, y, allLines, widths, xPeakValues, yPeakValues, snrs );

}

// Function for determining the scales we want to sample for the CWT calculation
//
// This function assumes the profile data is approximately (locally) equally spaced. If the spacing
// is much different then issues may arise. If exceptions are being thrown this would be the place to
// start looking. Check the Xspacing. For instance, if you pass a peak list to this function you'll
// get some unpredictable behavior.
void CwtPeakDetector::getScales( const vector <double> & mzData, const vector <double> & intensityData, 
                vector < vector< vector<int> > > & nPoints, vector <double> & widths ) const
{
    int mzLength = mzData.size();
    vector <double> Xspacing(mzLength,0.0);

    double lastXspacing = 0;
        
    // get the sampling rate as a function of m/z
    // this assumes that zero-intensity points flank the "islands" of data
    for (int i=1; i<mzLength-1; i++)
    {

        if ( intensityData[i] != 0.0 || intensityData[i-1] != 0.0 ) 
        {    
            Xspacing[i] = mzData[i] - mzData[i-1];
            if (Xspacing[i] <= 0)
                throw runtime_error("[CwtPeakDetector::getScales] m/z profile data are unsorted or contain duplicates");

            lastXspacing = Xspacing[i];
        }
        else
        {
            Xspacing[i] = lastXspacing;
        }

    }

    Xspacing[0] = Xspacing[1];
    Xspacing[mzLength-1] = Xspacing[mzLength-2];

    // I'm going to take a small average around each point b/c I worry that 
    // there may be cases where the Xspacing isn't as consistent as the files I've tested.
    // This might also be dangerous if a spectrum is extremely sparse and neighboring points
    // are separated by several hundred m/z, for example. However, the sampling rate is different
    // by about a factor of 3-4 from low to high m/z. I think we should be okay here.
    int window_size = 10;
    if (window_size > mzLength) window_size = mzLength / 2;
        
    int hf_window = window_size / 2;
    window_size = 2 * hf_window; // ensures consistency since original window_size could be odd

    for (int i=1; i<mzLength-1; i++)
    {

        int scalesToInclude = nScales;
        if ( intensityData[i] < 0.75*intensityData[i-1] || intensityData[i] < 0.75*intensityData[i+1] ) scalesToInclude = 1;

        int windowLow = i - hf_window; // inclusive
        if (windowLow<0) windowLow = 0;
        int windowHigh = i + hf_window; // not inclusive
        if (windowHigh>mzLength-1) windowHigh = mzLength - 1;
        // need to recalculate the window size in case we are at one of the edges
        int nTot = windowHigh - windowLow; // recall that windowHigh is not inclusive, so leave off the +1

        // get average
        double sum = accumulate( Xspacing.begin() + windowLow, Xspacing.begin() + windowHigh, 0.0);
        widths[i] = sum / double(nTot);

        // figure out the number of wavelet points you'll need to sample for each m/z point
        for (int j=0; j<scalesToInclude; ++j)
        {

            double maxMZwindow = widths[i] * scalings[j] * 3.0; // this returns the max possible m/z away from the current point where a wavelet may still contribute to the correlation

            int nPointsLeft = 0;
            int counter = i;
            while ( --counter >= 0  )
            {
                if ( mzData[i] - mzData[counter] > maxMZwindow ) break;
                nPointsLeft++;
            }

            int nPointsRight = 0;
            counter = i;
            while ( ++counter < mzLength  )
            {
                if ( mzData[counter] - mzData[i] > maxMZwindow ) break;
                nPointsRight++;
            }
        
            nPoints[0][j][i] = nPointsLeft;
            nPoints[1][j][i] = nPointsRight;

        }

    }
    

}
// end of function definition getScales


// Function for calculating the correlation matrix.
//
void CwtPeakDetector::calcCorrelation( const vector <double> & mz, const vector <double> & intensity, const vector < vector< vector<int> > > & waveletPoints,
                        const vector <double> & widths, vector < vector <double> > & matrix) const
{

    int mzLength = mz.size();

    // setup padded arrays for facilitating the wavelet computation
    int paddingPoints = 500; // should be more than sufficient
    vector <double> PadMz(mzLength+2*paddingPoints,0.0); // x array with padding on the front and back
    vector <double> PadIntensity(mzLength+2*paddingPoints,0.0); // y array with padding on the front and back
    for (int j = paddingPoints; j < mzLength + paddingPoints; j++)
    {
        PadMz[j] = mz[j-paddingPoints];
        PadIntensity[j] = intensity[j-paddingPoints];
    }
    vector <double> waveletData(paddingPoints,0.0);

    // calculate correlation between wavelet and spectrum data, populate correlation matrix
    for (int i = 0; i<nScales ; i++)
    {

        double currentScaling = scalings[i];
        int matrixIndex = 2; // This depends on the starting index in the loop immediately below!!

        for (int j = 1; j < mzLength-1; j++)
        { 

            // exclude points that are unlikely to have high correlation
            if ( i > 0 ) // calculate first row no matter what, as this is important for the noise calculation
            {
                if ( intensity[j] < 0.75*intensity[j-1] || intensity[j] < 0.75*intensity[j+1] )
                {
                    matrixIndex += 2;
                    continue;
                }
            }

            int nPointsLeft = waveletPoints[0][i][j];
            int nPointsRight = waveletPoints[1][i][j];
            

            int paddedCol = j + paddingPoints;
            double width = widths[j]*currentScaling;
            double param1 = 2.0 / ( sqrt(3.0 * width) * (sqrt( sqrt(3.141519) ) ) ); // ricker wavelet parameter
            double param2 = width * width; // ricker wavelet parameter


            ricker2d(PadMz, paddedCol, nPointsLeft, nPointsRight, param1, param2, PadMz[paddedCol], waveletData);


            int startPoint = paddedCol - nPointsLeft;
            for (int k = 0; k < nPointsLeft+nPointsRight+1 ; k++)
                matrix[i][matrixIndex] += waveletData[k] * PadIntensity[startPoint+k]; 

            matrixIndex++;

            if ( j == mzLength-1 ) break; // jump out before final iteration

            double moverzShift = ( PadMz[ paddedCol ] + PadMz[ paddedCol + 1 ] ) / 2.0;

            // calculate the correlation at the midpoint between two m/z points, as well. This is why
            // the length of the correlation matrix is (almost) twice that of the number of m/z points.
            ricker2d(PadMz, paddedCol, nPointsLeft, nPointsRight, param1, param2, moverzShift, waveletData);

            for (int k = 0; k < nPointsLeft+nPointsRight+1 ; k++)
                matrix[i][matrixIndex] += waveletData[k] * PadIntensity[startPoint+k];

            matrixIndex++;

        } // end for over mzPoints

    } // end for over 10 scales


}
// end of function calcCorrelation

void CwtPeakDetector::getPeakLines(const vector < vector <double> > & corrMatrix, const vector <double> & x, 
                                vector <ridgeLine> & allLines, vector <double> & snrs) const
{

    int corrMatrixLength = corrMatrix[0].size(); 

    // step 1
    vector < int > colMaxes(corrMatrixLength,0);
    for (int i=0; i<corrMatrixLength; ++i)
    {
        double corrMax = 0.0;
        for (int j=0; j<nScales; ++j)
        {

            if ( corrMatrix[j][i] > corrMax ) 
            {
                corrMax = corrMatrix[j][i];
                colMaxes[i] = j;
            }
        }
        
    }

    // step 2, setup bins of 300 points and calculate noise threshold within each bin
    double noise_per = 95.0;
    int window_size = 300;
    if (window_size > corrMatrixLength) window_size = corrMatrixLength / 2;
    int hf_window = window_size / 2;
    window_size = 2 * hf_window; // ensures consistency since original window_size could be odd

    int nNoiseBins = corrMatrixLength / window_size + 1;
    vector <double> noises(nNoiseBins,0.0);
    for (int i=0; i < nNoiseBins; ++i)
    {

        int windowLow = i * window_size; // inclusive
        int windowHigh = windowLow + window_size; // not inclusive
        if ( i == nNoiseBins - 1 ) windowHigh = corrMatrixLength;
        int nTot = windowHigh - windowLow; // don't need +1 because windowHigh is not inclusive

        vector <double> sortedData(nTot,0.0);
        for (int j = 0; j < nTot; j++) sortedData[j] = corrMatrix[0][windowLow + j]; // first row of correlation matrix

        // sort correlation data on first row within window using STL sort function
        sort(sortedData.begin(),sortedData.end());

        double noise = scoreAtPercentile( noise_per, sortedData, nTot );
        if ( noise < 1.0 ) noise = 1.0;

        noises[i] = noise;

    }

    vector <double> interpolatedXpoints(corrMatrixLength,0.0);
    for (int i=0; i<corrMatrixLength; ++i)
        interpolatedXpoints[i] = convertColToMZ( x, i );

    // step 3, find local maxima that are separated by at least mzTol_
    for (int i=2; i<corrMatrixLength-2; ++i)
    {

        double correlationVal = corrMatrix[colMaxes[i]][i];

        if ( correlationVal < corrMatrix[colMaxes[i-1]][i-1] ||
             correlationVal < corrMatrix[colMaxes[i-2]][i-2] ||
             correlationVal < corrMatrix[colMaxes[i+1]][i+1] ||
             correlationVal < corrMatrix[colMaxes[i+2]][i+2] ) continue;


        double mzCol = convertColToMZ( x, i );
        double lowTol = mzCol - mzTol_;
        double highTol = mzCol + mzTol_;

        // get the indices for the lower and upper bounds
        int lowBound = getColLowBound(interpolatedXpoints,lowTol);
        int highBound = getColHighBound(interpolatedXpoints,highTol);

        double maxCorr = 0.0;
        int maxCol = 0;
        for (int j=lowBound; j <= highBound; ++j)
        {
            int row = colMaxes[j];
            if ( corrMatrix[row][j] > maxCorr )
            {
                maxCorr = corrMatrix[row][j];
                maxCol = j;
            }

        }

        int noiseBin = maxCol / window_size;
        if ( noiseBin > nNoiseBins - 1 ) noiseBin = nNoiseBins - 1;
        double snr = maxCorr / noises[noiseBin];

        if ( snr < minSnr_ ) continue;

        int nLines = allLines.size();
        if ( nLines > 0 )
        {
            double mzNewLine = convertColToMZ(x,maxCol);
            double mzPrevLine = convertColToMZ(x,allLines[nLines-1].Col);
            double mzDiff = mzNewLine - mzPrevLine;
            double corrPrev = corrMatrix[allLines[nLines-1].Row][allLines[nLines-1].Col];
            if ( mzDiff > mzTol_ )
            {
                ridgeLine newLine;
                newLine.Col = maxCol;
                newLine.Row = colMaxes[maxCol];
                allLines.push_back(newLine);
                snrs.push_back(snr);
            }
            else if ( maxCorr > corrPrev )
            {
                // remove last ridge line
                allLines.pop_back();
                snrs.pop_back();
                // add new ridge line
                ridgeLine newLine;
                newLine.Col = maxCol;
                newLine.Row = colMaxes[maxCol];
                allLines.push_back(newLine);
                snrs.push_back(snr);
            }
        }
        else
        {
            ridgeLine newLine;
            newLine.Col = maxCol;
            newLine.Row = colMaxes[maxCol];
            allLines.push_back(newLine);
            snrs.push_back(snr);
        }

    }

}



void CwtPeakDetector::refinePeaks( const vector <double> & noisyX, const vector <double> & noisyY, const vector <ridgeLine> & lines,
                    const vector <double> & widths, vector <double> & smoothX, vector <double> & smoothY, vector <double> & snrs ) const
{

    if ( lines.size() == 0 ) return;
 
    for (int i=0, iend=lines.size(); i < iend; ++i)
    {

        double mzCol = convertColToMZ(noisyX,lines[i].Col);

        int row = lines[i].Row;
        double currentScaling = scalings[row];
        double offset = currentScaling * widths[lines[i].Col / 2];

        // get the indices for the lower and upper bounds that encapsulate the peak
        int startFittingPoint = getColLowBound(noisyX,mzCol-offset);
        int endFittingPoint = getColHighBound(noisyX,mzCol+offset);
        
        double maxIntensity = 0.0;
        double intensityAccumulator = 0.0;
        double mzCentroid = 0.0;
        double bestMZ = 0.0;

        // take weighted average of points in the peak to get centroid m/z
        if (centroid_)
        {
            for (int j = startFittingPoint; j <= endFittingPoint; ++j)
            {
                intensityAccumulator += noisyY[j];
                mzCentroid += noisyY[j]*noisyX[j];
                if (noisyY[j] >= maxIntensity)
                    maxIntensity = noisyY[j];
            }
            bestMZ = mzCentroid / intensityAccumulator;
        }
        // sum up the intensity and find the highest point. If there are multiple
        // points with the maxIntensity value, take the one with the highest m/z.
        else
        {
            for (int j = startFittingPoint; j <= endFittingPoint; ++j)
            {
                intensityAccumulator += noisyY[j];
                if (noisyY[j] >= maxIntensity)
                {
                    maxIntensity = noisyY[j];
                    bestMZ = noisyX[j];
                }
            }
        }

        if (smoothX.empty() || bestMZ != smoothX.back())
        {
            smoothX.push_back(bestMZ);
            smoothY.push_back(maxIntensity);
        }

    }

    // This is an intensity threshold filter for removing peaks with intensity of 1 
    // and the best matched wavelet at the lowest scale (indicitive of noise)
    for (int k = smoothX.size()-1; k > 0; --k)
    {
        if ( smoothY[k] < 2.0 && lines[k].Row < 1 )
        {
            smoothX.erase(smoothX.begin()+k);
            smoothY.erase(smoothY.begin()+k);
            snrs.erase(snrs.begin()+k);
        }
    }

    // peaks are occasionally out of order
    pwiz::util::sort_together(smoothX, vector<boost::iterator_range<vector<double>::iterator>> { smoothY, snrs });

    // possible to list the same peak if two lines are drawn on the same peak
    // and fall back to the same max intensity value (or a very similar max intensity value)
    for (int k = smoothX.size()-1; k > 0; --k)
    {
        if ( smoothX[k] - smoothX[k-1] < mzTol_ )
        {
            int removePeakIndex = smoothY[k] > smoothY[k-1] ? k-1 : k;
            smoothX.erase(smoothX.begin()+removePeakIndex);
            smoothY.erase(smoothY.begin()+removePeakIndex);
            snrs.erase(snrs.begin()+removePeakIndex);
        }
    }


    // If required, trim number of peaks to requested size.
    // This is used by Turbocharger.
    // it's important to do this after peak refinement, since replicated peaks may be removed at that point.
    if ( fixedPeaksKeep_ > 0 )
    {
        int sizeSNR = snrs.size();
        if ( sizeSNR > fixedPeaksKeep_ )
        {
            double updatedPercentLinesToFilter = 100.0*( 1.0 - ( (double)fixedPeaksKeep_ / (double)snrs.size() ) ); 
            vector <double> sortedSnrs = snrs;
            sort( sortedSnrs.begin(), sortedSnrs.end() );
            double cutoff = scoreAtPercentile(updatedPercentLinesToFilter,sortedSnrs,sortedSnrs.size());

            for (vector<double>::iterator it = snrs.begin(); it != snrs.end(); )
            {
                if ( *it < cutoff )
                {
                    int index1 = it - snrs.begin();
                    smoothX.erase(smoothX.begin()+index1);
                    smoothY.erase(smoothY.begin()+index1);
                    snrs.erase(it);
                }
                else
                {
                    ++it;
                }
            }
        }
    }
}

} // namespace analysis
} // namespace msdata


namespace {

// helper function that calculates the ricker (mexican hat) wavelet.
// Designed to handle irregularly spaced data.
void ricker2d(const vector <double> & Pad_mz, const int col, const int rickerPointsLeft, const int rickerPointsRight, 
                const double A, const double wsq, const double centralMZ, vector <double> & total)
{
	if (rickerPointsRight + rickerPointsLeft >= (int) total.size())
		throw runtime_error("[CwtPeakDetector::ricker2d] invalid input parameters");

    for (int i = col-rickerPointsLeft, cnt=0, end = col+rickerPointsRight; i <= end; i++, cnt++) 
    {
            double vec = Pad_mz[i] - centralMZ;
            double tsq = vec * vec;
            double mod = 1.0 - tsq / wsq;
            double gauss = exp( -1.0 * tsq / (2.0 * wsq) );
            total[cnt] = A * mod * gauss;
    }

}
// end of function ricker2d

// want first point to the right of target
int getColLowBound(const vector <double> & mzs,const double target)
{
    const vector<double>::const_iterator it = lower_bound(mzs.begin(),mzs.end(),target);
    return it - mzs.begin();
}

// want first point the the left of target
int getColHighBound(const vector <double> & mzs,const double target)
{
    const vector<double>::const_iterator it = upper_bound(mzs.begin(),mzs.end(),target);
    const vector<double>::const_iterator decrementedIt = it - 1; // decrement by one to get the value that is less than target
    return decrementedIt - mzs.begin();
}


// function for determining the score in a (sorted) vector at a given percentile
// end of getScoreAtPercentile. Allow passing of length of vector in case you only 
// want a slice of the first portion of a vector;
//
// perc should not be a fraction (e.g. 5th percentile = 5.0)
double scoreAtPercentile( const double perc, const vector < double > & dataSorted, const int nTot )
{
    //using nTot - 1, which is what's done in scipy.scoreatpercentile
    double nBelow = (double)(nTot-1) * perc / 100.0;

    if (ceil(nBelow) == nBelow) // whole number
    {
        return dataSorted[(int)nBelow]; // consistent with scipy.scoreatpercentile
    }
    else
    {
        // fraction method used in scipy.scoreatpercentile
        // linear interpolation between two points 
        double loInd = floor(nBelow);
        double hiInd = ceil(nBelow);
        double fraction = nBelow - loInd;
        double lo = dataSorted[(int)loInd];
        double hi = dataSorted[(int)hiInd];
        return lo + (hi - lo) * fraction;
    }
}
// end function scoreAtPercentile

double convertColToMZ( const vector <double> & mzs, const int Col )
{
    int mapIndex = Col / 2;
    if ( Col % 2 == 1 )
        return (mzs[ mapIndex ]+mzs[mapIndex+1]) / 2.0;
    else
        return mzs[ mapIndex ];
}

} // namespace
