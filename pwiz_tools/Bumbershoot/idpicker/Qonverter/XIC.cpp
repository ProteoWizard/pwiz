//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

#include "Embedder.hpp"
#include "Qonverter.hpp"
#include "SchemaUpdater.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "boost/foreach_field.hpp"
#include "boost/throw_exception.hpp"
#include "boost/xpressive/xpressive.hpp"

BEGIN_IDPICKER_NAMESPACE
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace pwiz::chemistry;
//namespace sqlite = sqlite;
using namespace Embedder;
using pwiz::chemistry::MZTolerance;

namespace XIC {

#ifdef WIN32
const string defaultSourceExtensionPriorityList("mz5;mzML;mzXML;RAW;WIFF;d;t2d;ms2;cms2;mgf");
#else
const string defaultSourceExtensionPriorityList("mz5;mzML;mzXML;ms2;cms2;mgf");
#endif

XICConfiguration::XICConfiguration(bool AlignRetentionTime, double MaxQValue,
                     int MonoisotopicAdjustmentMin, int MonoisotopicAdjustmentMax,
                     int RetentionTimeLowerTolerance, int RetentionTimeUpperTolerance,
                     MZTolerance ChromatogramMzLowerOffset, MZTolerance ChromatogramMzUpperOffset)
                     :AlignRetentionTime(AlignRetentionTime), MaxQValue(MaxQValue), MonoisotopicAdjustmentMin(MonoisotopicAdjustmentMin), MonoisotopicAdjustmentMax(MonoisotopicAdjustmentMax), RetentionTimeLowerTolerance(RetentionTimeLowerTolerance), RetentionTimeUpperTolerance(RetentionTimeUpperTolerance), ChromatogramMzLowerOffset(ChromatogramMzLowerOffset), ChromatogramMzUpperOffset(ChromatogramMzUpperOffset)
{}

double pchst ( double arg1, double arg2 )
{
  double value;

  if ( arg1 == 0.0 )
  {
    value = 0.0;
  }
  else if ( arg1 < 0.0 )
  {
    if ( arg2 < 0.0 )
    {
      value = 1.0;
    }
    else if ( arg2 == 0.0 )
    {
      value = 0.0;
    }
    else if ( 0.0 < arg2 )
    {
      value = -1.0;
    }
  }
  else if ( 0.0 < arg1 )
  {
    if ( arg2 < 0.0 )
    {
      value = -1.0;
    }
    else if ( arg2 == 0.0 )
    {
      value = 0.0;
    }
    else if ( 0.0 < arg2 )
    {
      value = 1.0;
    }
  }

  return value;
}

void spline_pchip_set ( int n, double x[], double f[], double d[] )
{
    double del1;
  double del2;
  double dmax;
  double dmin;
  double drat1;
  double drat2;
  double dsave;
  double h1;
  double h2;
  double hsum;
  double hsumt3;
  int i;
  int ierr;
  int nless1;
  double temp;
  double w1;
  double w2;
//
//  Check the arguments.
//
  if ( n < 2 )
  {
    throw runtime_error(string("[SPLINE_PCHIP_SET]- Number of data points less than 2"));
  }

  for ( i = 1; i < n; i++ )
  {
    if ( x[i] <= x[i-1] )
    {
      throw runtime_error(string("[SPLINE_PCHIP_SET]- X array not strictly increasing"));
    }
  }

  ierr = 0;
  nless1 = n - 1;
  h1 = x[1] - x[0];
  del1 = ( f[1] - f[0] ) / h1;
  dsave = del1;
//
//  Special case N=2, use linear interpolation.
//
  if ( n == 2 )
  {
    d[0] = del1;
    d[n-1] = del1;
    return;
  }
//
//  Normal case, 3 <= N.
//
  h2 = x[2] - x[1];
  del2 = ( f[2] - f[1] ) / h2;
//
//  Set D(1) via non-centered three point formula, adjusted to be
//  shape preserving.
//
  hsum = h1 + h2;
  w1 = ( h1 + hsum ) / hsum;
  w2 = -h1 / hsum;
  d[0] = w1 * del1 + w2 * del2;

  if ( pchst ( d[0], del1 ) <= 0.0 )
  {
    d[0] = 0.0;
  }
//
//  Need do this check only if monotonicity switches.
//
  else if ( pchst ( del1, del2 ) < 0.0 )
  {
     dmax = 3.0 * del1;

     if ( fabs ( dmax ) < fabs ( d[0] ) )
     {
       d[0] = dmax;
     }

  }
//
//  Loop through interior points.
//
  for ( i = 2; i <= nless1; i++ )
  {
    if ( 2 < i )
    {
      h1 = h2;
      h2 = x[i] - x[i-1];
      hsum = h1 + h2;
      del1 = del2;
      del2 = ( f[i] - f[i-1] ) / h2;
    }
//
//  Set D(I)=0 unless data are strictly monotonic.
//
    d[i-1] = 0.0;

    temp = pchst ( del1, del2 );

    if ( temp < 0.0 )
    {
      ierr = ierr + 1;
      dsave = del2;
    }
//
//  Count number of changes in direction of monotonicity.
//
    else if ( temp == 0.0 )
    {
      if ( del2 != 0.0 )
      {
        if ( pchst ( dsave, del2 ) < 0.0 )
        {
          ierr = ierr + 1;
        }
        dsave = del2;
      }
    }
//
//  Use Brodlie modification of Butland formula.
//
    else
    {
      hsumt3 = 3.0 * hsum;
      w1 = ( hsum + h1 ) / hsumt3;
      w2 = ( hsum + h2 ) / hsumt3;
      if (fabs ( del1 ) > fabs ( del2 ))
      {
        dmax = fabs ( del1 );
        dmin = fabs ( del2 );
      }
      else
      {
        dmax = fabs ( del2 );
        dmin = fabs ( del1 );
      }
      drat1 = del1 / dmax;
      drat2 = del2 / dmax;
      d[i-1] = dmin / ( w1 * drat1 + w2 * drat2 );
    }
  }
//
//  Set D(N) via non-centered three point formula, adjusted to be
//  shape preserving.
//
  w1 = -h2 / hsum;
  w2 = ( h2 + hsum ) / hsum;
  d[n-1] = w1 * del1 + w2 * del2;

  if ( pchst ( d[n-1], del2 ) <= 0.0 )
  {
    d[n-1] = 0.0;
  }
  else if ( pchst ( del1, del2 ) < 0.0 )
  {
//
//  Need do this check only if monotonicity switches.
//
    dmax = 3.0 * del2;

    if ( fabs ( dmax ) < abs ( d[n-1] ) )
    {
      d[n-1] = dmax;
    }

  }
  return;
}

 int chfev ( double x1, double x2, double f1, double f2, double d1, double d2,
   int ne, double xe[], double fe[], int next[] )
  {
       double c2;
   double c3;
   double del1;
   double del2;
   double delta;
   double h;
   int i;
   int ierr;
   double x;
   double xma;
   double xmi;

   if ( ne < 1 )
   {
     ierr = -1;
     throw runtime_error(string("[chfev]- Number of evaluation points is less than 1"));
   }

   h = x2 - x1;

   if ( h == 0.0 )
   {
     ierr = -2;
     throw runtime_error(string("[chfev]- The interval [X1,X2] is of zero length"));
   }
 //
 //  Initialize.
 //
   ierr = 0;
   next[0] = 0;
   next[1] = 0;
   if (h > 0)
   {
       xmi = 0;
       xma = h;
   }
   else
   {
       xmi = 0;
       xma = h;
   }
 //
 //  Compute cubic coefficients expanded about X1.
 //
   delta = ( f2 - f1 ) / h;
   del1 = ( d1 - delta ) / h;
   del2 = ( d2 - delta ) / h;
   c2 = -( del1 + del1 + del2 );
   c3 = ( del1 + del2 ) / h;
 //
 //  Evaluation loop.
 //
   for ( i = 0; i < ne; i++ )
   {
     x = xe[i] - x1;
     fe[i] = f1 + x * ( d1 + x * ( c2 + x * c3 ) );
 //
 //  Count the extrapolation points.
 //
     if ( x < xmi )
     {
       next[0] = next[0] + 1;
     }

     if ( xma < x )
     {
       next[1] = next[1] + 1;
     }

   }

   return 0;
  }

void spline_pchip_val ( int n, double x[], double f[], double d[],
  int ne, double xe[], double fe[] )
{
      int i;
  int ierc;
  int ierr;
  int ir;
  int j;
  int j_first;
  int j_new;
  int j_save;
  int next[2];
  int nj;
//
//  Check arguments.
//
  if ( n < 2 )
  {
    ierr = -1;
    throw runtime_error(string("[spline_pchip_val]- Number of data points less than 2"));
  }

  for ( i = 1; i < n; i++ )
  {
    if ( x[i] <= x[i-1] )
    {
      ierr = -3;
      throw runtime_error(string("[spline_pchip_val]- X array not strictly increasing- "+ lexical_cast<string>(i) + " ; "+ lexical_cast<string>(n) + " ; " + lexical_cast<string>(x[i]) + " <= " + lexical_cast<string>(x[i-1])));
    }
  }

  if ( ne < 1 )
  {
    ierr = -4;
    return;
  }

  ierr = 0;
//
//  Loop over intervals.
//  The interval index is IL = IR-1.
//  The interval is X(IL) <= X < X(IR).
//
  j_first = 1;
  ir = 2;

  for ( ; ; )
  {
//
//  Skip out of the loop if have processed all evaluation points.
//
    if ( ne < j_first )
    {
      break;
    }
//
//  Locate all points in the interval.
//
    j_save = ne + 1;

    for ( j = j_first; j <= ne; j++ )
    {
      if ( x[ir-1] <= xe[j-1] )
      {
        j_save = j;
        if ( ir == n )
        {
          j_save = ne + 1;
        }
        break;
      }
    }
//
//  Have located first point beyond interval.
//
    j = j_save;

    nj = j - j_first;
//
//  Skip evaluation if no points in interval.
//
    if ( nj != 0 )
    {
//
//  Evaluate cubic at XE(J_FIRST:J-1).
//
      ierc = chfev ( x[ir-2], x[ir-1], f[ir-2], f[ir-1], d[ir-2], d[ir-1],
        nj, xe+j_first-1, fe+j_first-1, next );

      if ( ierc < 0 )
      {
        ierr = -5;
        throw runtime_error(string("[spline_pchip_val]- Error return from CHFEV (" + lexical_cast<string>(ierr) + ")"));
      }
//
//  In the current set of XE points, there are NEXT(2) to the right of X(IR).
//
      if ( next[1] != 0 )
      {
        if ( ir < n )
        {
          ierr = -5;
          throw runtime_error(string("[spline_pchip_val]-   IR < N"));
        }
//
//  These are actually extrapolation points.
//
        ierr = ierr + next[1];

      }
//
//  In the current set of XE points, there are NEXT(1) to the left of X(IR-1).
//
      if ( next[0] != 0 )
      {
//
//  These are actually extrapolation points.
//
        if ( ir <= 2 )
        {
          ierr = ierr + next[0];
        }
        else
        {
          j_new = -1;

          for ( i = j_first; i <= j-1; i++ )
          {
            if ( xe[i-1] < x[ir-2] )
            {
              j_new = i;
              break;
            }
          }

          if ( j_new == -1 )
          {
            ierr = -5;
            throw runtime_error(string("[spline_pchip_val]- Could not bracket the data point"));
          }
//
//  Reset J.  This will be the new J_FIRST.
//
          j = j_new;
//
//  Now find out how far to back up in the X array.
//
          for ( i = 1; i <= ir-1; i++ )
          {
            if ( xe[j-1] < x[i-1] )
            {
              break;
            }
          }
//
//  At this point, either XE(J) < X(1) or X(i-1) <= XE(J) < X(I) .
//
//  Reset IR, recognizing that it will be incremented before cycling.
//
          if (i-1 > 1)
            ir = i-1;
          else
            ir = 1;
        }
      }

      j_first = j;
    }

    ir = ir + 1;

    if ( n < ir )
    {
      break;
    }

  }
}

XICWindowList GetMZRTWindows(sqlite::database& db, MS2ScanMap& ms2ScanMap, const string& sourceId, XICConfiguration config)
{
    bool useAvgMass = (config.MonoisotopicAdjustmentMin==0) && (config.MonoisotopicAdjustmentMax==0);
    string deltaMassColumn = useAvgMass ? "AvgMassDelta" : "MonoMassDelta";
    string massColumn = useAvgMass ? "MolecularWeight" : "MonoisotopicMass";
    string sql = "SELECT psm.Id, psm.Peptide, Source, NativeID, PrecursorMZ, Charge, IFNULL(Mods, '') AS Mods, Qvalue,  "
                "(IFNULL(TotalModMass,0)+pep."+massColumn+"+Charge*1.0076)/Charge AS ExactMZ,   "
                "IFNULL(SUBSTR(Sequence, pi.Offset+1, pi.Length),DecoySequence) || ' ' || Charge || ' ' ||  IFNULL(Mods, '') as Distinct_Match   "
                "FROM Spectrum s  "
                "JOIN PeptideSpectrumMatch psm ON s.Id=psm.Spectrum  "
                "JOIN Peptide pep ON psm.Peptide=pep.Id  "
                "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide  "
                "LEFT JOIN ProteinData pro ON pi.Protein=pro.Id  "
                "LEFT JOIN (SELECT pm.PeptideSpectrumMatch AS PsmId,  "
                "GROUP_CONCAT(MonoMassDelta || '@' || pm.Offset) AS Mods,  "
                "SUM("+deltaMassColumn+") AS TotalModMass  "
                "FROM PeptideModification pm  "
                "JOIN Modification mod ON pm.Modification=mod.Id  "
                "GROUP BY PsmId) mods2 ON psm.Id=mods2.PsmId  "
                "WHERE QValue <= ? AND Source=? AND Rank=1  "
                //"WHERE Source=? AND Rank=1 AND psmScore.ScoreNameId = "+selectedScore+"  "
                "GROUP BY psm.Id  "
                "ORDER BY Distinct_Match, Charge, Mods ";



    sqlite::query q(db, sql.c_str());
    //q.binder() << config.MaxQValue << sourceId;
    q.binder() << config.MaxQValue << sourceId;

    XICWindowList windows;
    XICWindow tmpWindow;
    XICPeptideSpectrumMatch tmpPSM;
    sqlite_int64 lastPeptide = 0;
    int lastCharge = 0;
    string lastModif = "initial value";
    SortByScanTime sortByScanTime;

    string currentMatch = "";
    BOOST_FOREACH(sqlite::query::rows row, q)
    {
        sqlite_int64 psmId;
        sqlite_int64 peptide;
        char const* id;
        double precursorMZ;
        int charge;
        string modif;
        double score, exactMZ;
        string sourceId;
        string distinctMatch;

        row.getter() >> psmId >> peptide >> sourceId >> id >> precursorMZ >> charge >> modif >> score >> exactMZ >> distinctMatch;

        MS2ScanMap::index<nativeID>::type::const_iterator itr = ms2ScanMap.get<nativeID>().find(id);
        if (itr == ms2ScanMap.get<nativeID>().end())
            throw runtime_error(string("PSM identified to spectrum \"") + id + "\" is not in the scan map");

        if (itr->msLevel > 2)
            continue;

        ms2ScanMap.get<nativeID>().modify(itr, ModifyPrecursorMZ(precursorMZ)); // prefer corrected monoisotopic m/z
        const MS2ScanInfo& scanInfo = *itr;

        if (distinctMatch != currentMatch)
        { // if any of these values change we make a new chromatogram
        
            if (!tmpWindow.PSMs.empty())
            {
                sort(tmpWindow.PSMs.begin(), tmpWindow.PSMs.end(), sortByScanTime);
                tmpWindow.meanMS2RT /= tmpWindow.PSMs.size();
                windows.insert(tmpWindow);
            }
                
            currentMatch = distinctMatch;//distinctMatchId;
            lastPeptide = peptide;
            lastCharge = tmpPSM.charge = charge;
            lastModif = modif;
            tmpPSM.exactMZ = exactMZ;
            tmpWindow.source = sourceId;
            tmpWindow.distinctMatch=distinctMatch;
            tmpWindow.firstMS2RT = tmpWindow.lastMS2RT = tmpWindow.meanMS2RT = scanInfo.scanStartTime;
            tmpWindow.bestScore = score;
            tmpWindow.bestScoreScanStartTime = scanInfo.scanStartTime;
            tmpWindow.PSMs.clear();
            tmpWindow.preRT.clear();
            tmpWindow.preMZ.clear();

            //IntegerSet::const_iterator itr = g_rtConfig->MonoisotopeAdjustmentSet.begin();
            //for (; itr != g_rtConfig->MonoisotopeAdjustmentSet.end(); ++itr) //access with *itr
            for (int x = config.MonoisotopicAdjustmentMin; x <= config.MonoisotopicAdjustmentMax; x++)
            {
                if (tmpPSM.charge == 0) throw runtime_error("[chromatogramMzWindow] charge cannot be 0");
                double centerMz = tmpPSM.exactMZ + x * Neutron / tmpPSM.charge;
                double mzLower = centerMz - MZTolerance(config.ChromatogramMzLowerOffset.value * tmpPSM.charge, config.ChromatogramMzLowerOffset.units);
                double mzUpper = centerMz + MZTolerance(config.ChromatogramMzUpperOffset.value * tmpPSM.charge, config.ChromatogramMzUpperOffset.units);
                tmpWindow.preMZ += boost::icl::interval_set<double>(continuous_interval<double>::closed(mzLower, mzUpper));
            }
        


            if (!scanInfo.identified)
                throw runtime_error("PSM " + scanInfo.nativeID + " is not identified (should never happen)");

            if (!boost::icl::contains(tmpWindow.preMZ, scanInfo.precursorMZ))
            {
                //fileOut << "§Warning: PSM for spectrum \"" << scanInfo.nativeID << "\" with observed m/z " << scanInfo.precursorMZ << " is disjoint with the exact m/z " << tmpPSM.exactMZ << endl;
                continue;
            }
        }

        tmpPSM.id = psmId;
        tmpPSM.peptide = peptide;
        tmpPSM.spectrum = &scanInfo;
        tmpPSM.score = score;
        tmpWindow.PSMs.push_back(tmpPSM);

        tmpWindow.firstMS2RT = min(tmpWindow.firstMS2RT, scanInfo.scanStartTime);
        tmpWindow.lastMS2RT = max(tmpWindow.lastMS2RT, scanInfo.scanStartTime);
        tmpWindow.meanMS2RT += scanInfo.scanStartTime;
        if (tmpPSM.score < tmpWindow.bestScore)
        {
            tmpWindow.bestScore = tmpPSM.score;
            tmpWindow.bestScoreScanStartTime = scanInfo.scanStartTime;
        }
        tmpWindow.preRT += boost::icl::interval_set<double>(continuous_interval<double>::closed(scanInfo.precursorScanStartTime-config.RetentionTimeLowerTolerance, scanInfo.precursorScanStartTime+config.RetentionTimeUpperTolerance));
    }

    if (!tmpWindow.PSMs.empty())
    {
        sort(tmpWindow.PSMs.begin(), tmpWindow.PSMs.end(), sortByScanTime);
        tmpWindow.meanMS2RT /= tmpWindow.PSMs.size();
            windows.insert(tmpWindow); // add the last peptide to the vector
    }
    return windows;
}
//
void simulateGaussianPeak(double peakStart, double peakEnd,
                              double peakHeight, double peakBaseline,
                              double mean, double stddev,
                              size_t samples,
                              vector<double>& x, vector<double>& y)
{
    using namespace boost::math;
    normal_distribution<double> peakDistribution(mean, stddev);
    x.push_back(peakStart); y.push_back(peakBaseline);
    peakStart += numeric_limits<double>::epsilon();
    double sampleRate = (peakEnd - peakStart) / samples;
    double scale = peakHeight / pdf(peakDistribution, mean);
    for (size_t i=0; i <= samples; ++i)
    {
        x.push_back(peakStart + sampleRate*i);
        y.push_back(peakBaseline + scale * pdf(peakDistribution, x.back()));
    }
    x.push_back(peakEnd); y.push_back(peakBaseline);
}

int writeChromatograms(const string& idpDBFilename,
                            const XICWindowList& pepWindow,
                            const vector<RegDefinedPrecursorInfo>& RegDefinedPrecursors,
                            pwiz::util::IterationListenerRegistry* ilr,
                            const int& currentFile, const int& totalFiles)
{
    int totalAdded = 0;
    try
    {
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "Writing chromatograms to idpDB");
        shared_ptr<ChromatogramListSimple> chromatogramListSimple(new ChromatogramListSimple);

        sqlite::database idpDB(idpDBFilename);
        idpDB.execute("PRAGMA journal_mode=OFF;"
                      "PRAGMA synchronous=OFF;"
                      "PRAGMA cache_size=50000;"
                      IDPICKER_SQLITE_PRAGMA_MMAP);

        sqlite::transaction transaction(idpDB);
        //initialize the table
        idpDB.execute("CREATE TABLE IF NOT EXISTS XICMetrics (PsmId INTEGER PRIMARY KEY, PeakIntensity NUMERIC, PeakArea NUMERIC, PeakSNR NUMERIC, PeakTimeInSeconds NUMERIC);");
        string sql = "INSERT INTO XICMetrics (PsmId, PeakIntensity, PeakArea, PeakSNR, PeakTimeInSeconds) values (?,?,?,?,?)";
        sqlite::command insertPeptideIntensity(idpDB, sql.c_str());

        // Put unique identified peptide chromatograms first in the file
        double totalBestPeaks = 0;
        BOOST_FOREACH(const XICWindow& window, pepWindow)
        {
            if (window.MS1RT.empty())
                continue;
            chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
            Chromatogram& c = *chromatogramListSimple->chromatograms.back();
            c.index = chromatogramListSimple->size()-1;
            ostringstream oss;
            oss << " (id: " << window.PSMs[0].peptide
                << "; m/z: " << window.preMZ
                << "; time: " << window.preRT << ")";
            c.id = "Raw SIC for " + oss.str();
            c.set(MS_SIC_chromatogram);
            c.setTimeIntensityArrays(window.MS1RT, window.MS1Intensity, UO_second, MS_number_of_detector_counts);



            CrawdadPeakFinder crawdadPeakFinder;
            crawdadPeakFinder.SetChromatogram(window.MS1RT, window.MS1Intensity);

            chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
            Chromatogram& c3 = *chromatogramListSimple->chromatograms.back();
            c3.index = chromatogramListSimple->size()-1;
            c3.id = "Smoothed SIC for " + oss.str();
            c3.set(MS_SIC_chromatogram);
            double sampleRate = window.MS1RT[1] - window.MS1RT[0];
            size_t wingSize = crawdadPeakFinder.getWingData().size();
            vector<double> newRT(wingSize, 0);
            for(size_t i=0; i < wingSize; ++i) newRT[i] = window.MS1RT[0] - (wingSize-i)*sampleRate;
            newRT.insert(newRT.end(), window.MS1RT.begin(), window.MS1RT.end());
            for(size_t i=1; i <= wingSize; ++i) newRT.push_back(window.MS1RT.back() + i*sampleRate);
            const vector<float>& tmp = crawdadPeakFinder.getSmoothedIntensities();
            if (tmp.size() == newRT.size())
                c3.setTimeIntensityArrays(newRT, vector<double>(tmp.begin(), tmp.end()), UO_second, MS_number_of_detector_counts);

            float baselineIntensity = crawdadPeakFinder.getBaselineIntensity();

            // output all Crawdad peaks
            {
                chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& c2 = *chromatogramListSimple->chromatograms.back();
                c2.index = chromatogramListSimple->size()-1;
                c2.id = "Crawdad peaks for " + oss.str();
                c2.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);

                BOOST_FOREACH(const Peak& peak, window.peaks)
                {
                    simulateGaussianPeak(peak.startTime, peak.endTime,
                                         peak.intensity, baselineIntensity,
                                         peak.peakTime, peak.fwhm / 2.35482,
                                         50,
                                         c2.getTimeArray()->data, c2.getIntensityArray()->data);
                }
            }

            // output only the best peak
            if (window.bestPeak)
            {
            totalBestPeaks +=1;
                chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& c2 = *chromatogramListSimple->chromatograms.back();
                c2.index = chromatogramListSimple->size()-1;
                c2.id = "Best Crawdad peak for " + oss.str();
                c2.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);

                const Peak& peak = *window.bestPeak;
                simulateGaussianPeak(peak.startTime, peak.endTime,
                                     peak.intensity, baselineIntensity,
                                     peak.peakTime, peak.fwhm / 2.35482,
                                     50,
                                     c2.getTimeArray()->data, c2.getIntensityArray()->data);
                //cout<<oss.str()<<" intensity: "<< peak.intensity<<endl;


                const vector<double>& myIntensityVec=window.MS1Intensity;
                /*sort(myIntensityVec.begin(),myIntensityVec.end());
                vector<double>::iterator it;
                it=myIntensityVec.begin();
                double peakPrecursorIntensityMedian=*(it+=(myIntensityVec.size()/2));


                double SNRatio=(peak.intensity/peakPrecursorIntensityMedian) ;*/
                const vector<double>& times=window.MS1RT;
                accs::accumulator_set<double, accs::stats<accs::tag::variance, accs::tag::mean> > PeakIntensities;
                accs::accumulator_set<double, accs::stats<accs::tag::variance, accs::tag::mean> > BackgroundIntensities;
                  for (size_t  i = 0; i < myIntensityVec.size(); i++){
                      if(times[i]>=peak.startTime&&times[i]<=peak.endTime){
                         PeakIntensities(myIntensityVec[i]);
                      }
                      else{
                          BackgroundIntensities(myIntensityVec[i]);
                      }
                  }
                double peak_mean=accs::mean(PeakIntensities);
                double bcd_mean=accs::mean(BackgroundIntensities);
                double peak_var=accs::variance(PeakIntensities);
                double bcd_var=accs::variance(BackgroundIntensities);
                double SNRatio=(peak_mean-bcd_mean)/ sqrt(peak_var+bcd_var);

                double peakArea=peak.intensity*peak.fwhm;

                //HACK: only report once for each PSM to avoid duplicate distinct matches
                // this is for the sake of reporting in IDP3
                //BOOST_FOREACH(XICPeptideSpectrumMatch psm, window.PSMs)
                if (window.PSMs.size() > 0)
                {
                    //insertPeptideIntensity.binder()<<psm.id<<window.distinctMatch<<window.source<<peak.intensity<<peakArea<<SNRatio<<peak.peakTime;
                    insertPeptideIntensity.binder() << lexical_cast<string>(window.PSMs[0].id) << lexical_cast<string>(peak.intensity)
                                                    << lexical_cast<string>(peakArea) << lexical_cast<string>(SNRatio) << lexical_cast<string>(peak.peakTime);
                    insertPeptideIntensity.execute();
                    insertPeptideIntensity.reset();
                    totalAdded++;
                }
                //cout<<c2.getTimeArray()->data<<endl;
            }
        }
        transaction.commit();
    }
    catch (exception& e)
    {
        throw runtime_error(string("[writeChromatograms] ") + e.what());
    }
    return totalAdded;
}

int EmbedMS1ForFile(sqlite::database& idpDb, const string& idpDBFilePath, const string& sourceFilePath, const string& sourceId, XICConfiguration& config, pwiz::util::IterationListenerRegistry* ilr, const int& currentFile, const int& totalFiles)
{
    try
    {

        string sourceFilename = bfs::path(sourceFilePath).filename().string();
        // Initialize the idpicker reader. It supports idpDB's for now.
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "Reading identifications from " + sourceFilename);

        // Obtain the list of readers available
        ExtendedReaderList readers;
        MSDataFile msd(sourceFilePath, &readers);

        //TODO: add ability to apply spectrum list filters
        /*vector<string> wrappers;
        SpectrumListFactory::wrap(msd, wrappers);*/

        SpectrumList& spectrumList = *msd.run.spectrumListPtr;

        //ITERATION_UPDATE(ilr, currentFile, totalFiles, "Started processing file " + sourceFilename);

        // Spectral counts
        int MS1Count = 0, MS2Count = 0;

        MS1ScanMap ms1ScanMap;
        MS2ScanMap ms2ScanMap;

        map<string, string> tempMap;
        {
            string sql = "SELECT s.NativeID, IFNULL(SUBSTR(Sequence, pi.Offset+1, pi.Length),DecoySequence) || ' ' || Charge || ' ' ||  IFNULL(Mods, '') as Distinct_Match   "
                             "FROM PeptideSpectrumMatch psm "
                             "JOIN Spectrum s ON psm.Spectrum=s.Id "
                             "JOIN Peptide pep ON psm.Peptide=pep.Id  "
                             "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide  "
                             "LEFT JOIN ProteinData pro ON pi.Protein=pro.Id  "
                             "LEFT JOIN (SELECT pm.PeptideSpectrumMatch AS PsmId,  "
                             "GROUP_CONCAT(MonoMassDelta || '@' || pm.Offset) AS Mods,  "
                             "SUM(MonoMassDelta) AS TotalModMass  "
                             "FROM PeptideModification pm  "
                             "JOIN Modification mod ON pm.Modification=mod.Id  "
                             "GROUP BY PsmId) mods2 ON psm.Id=mods2.PsmId  "
                             "WHERE Qvalue<=0.05 AND Rank=1 AND Source=? "
                             "GROUP BY s.Id ";

            sqlite::query distinctModifiedPeptideByNativeIDQuery(idpDb, sql.c_str());

            distinctModifiedPeptideByNativeIDQuery.binder() << sourceId;

            char const* nativeId;
            string distinctMatch;

            int temp = 0;
            BOOST_FOREACH(sqlite::query::rows row, distinctModifiedPeptideByNativeIDQuery)
            {
                temp++;
                row.getter() >> nativeId >> distinctMatch;
                tempMap[nativeId] = distinctMatch;
            }
        }
        const map<string, string>& distinctModifiedPeptideByNativeID = tempMap;
        map<string, double> firstScanTimeOfDistinctModifiedPeptide;

        string lastMS1NativeId;
        size_t missingPrecursorIntensities = 0;
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "Found " + lexical_cast<string>(spectrumList.size())+ " spectra, reading metadata...");

        // For each spectrum
        for( size_t curIndex = 0; curIndex < spectrumList.size(); ++curIndex )
        {
            SpectrumPtr spectrum = spectrumList.spectrum(curIndex, false);

            if (spectrum->defaultArrayLength == 0)
                continue;

            if (spectrum->cvParam(MS_MSn_spectrum).empty() && spectrum->cvParam(MS_MS1_spectrum).empty())
                continue;

            CVParam spectrumMSLevel = spectrum->cvParam(MS_ms_level);
            if (spectrumMSLevel == CVID_Unknown)
                continue;

            // Check its MS level and increment the count
            int msLevel = spectrumMSLevel.valueAs<int>();
            if (msLevel == 1)
            {
                MS1ScanInfo scanInfo;
                lastMS1NativeId = scanInfo.nativeID = spectrum->id;
                scanInfo.totalIonCurrent = spectrum->cvParam(MS_total_ion_current).valueAs<double>();

                if (spectrum->scanList.scans.empty())
                    throw runtime_error("No scan start time for " + spectrum->id);

                Scan& scan = spectrum->scanList.scans[0];
                CVParam scanTime = scan.cvParam(MS_scan_start_time);
                if (scanTime.empty())
                    throw runtime_error("No scan start time for " + spectrum->id);

                scanInfo.scanStartTime = scanTime.timeInSeconds();

                ms1ScanMap.push_back(scanInfo);
                ++MS1Count;
            }
            else if (msLevel == 2)
            {
                MS2ScanInfo scanInfo;
                scanInfo.nativeID = spectrum->id;
                scanInfo.msLevel = 2;

                if (spectrum->precursors.empty() || spectrum->precursors[0].selectedIons.empty())
                    throw runtime_error("No selected ion found for MS2 " + spectrum->id);

                Precursor& precursor = spectrum->precursors[0];
                const SelectedIon& si = precursor.selectedIons[0];

                scanInfo.precursorIntensity = si.cvParam(MS_peak_intensity).valueAs<double>();
                if (scanInfo.precursorIntensity == 0)
                {
                    //throw runtime_error("No precursor intensity for MS2 " + spectrum->id);
                    //cerr << "\nNo precursor intensity for MS2 " + spectrum->id << endl;
                    ++missingPrecursorIntensities;

                    // fall back on MS2 TIC
                    scanInfo.precursorIntensity = spectrum->cvParam(MS_total_ion_current).valueAs<double>();
                }

                if (precursor.spectrumID.empty())
                {
                    if (lastMS1NativeId.empty())
                        throw runtime_error("No MS1 spectrum found before " + spectrum->id);
                    scanInfo.precursorNativeID = lastMS1NativeId;
                }
                else
                    scanInfo.precursorNativeID = precursor.spectrumID;

                if (spectrum->scanList.scans.empty())
                    throw runtime_error("No scan start time for " + spectrum->id);

                Scan& scan = spectrum->scanList.scans[0];
                CVParam scanTime = scan.cvParam(MS_scan_start_time);
                if (scanTime.empty())
                    throw runtime_error("No scan start time for " + spectrum->id);
                scanInfo.scanStartTime = scanTime.timeInSeconds();

                ++MS2Count;

                scanInfo.precursorMZ = si.cvParam(MS_selected_ion_m_z).valueAs<double>();
                if (si.cvParam(MS_selected_ion_m_z).empty() )
                    scanInfo.precursorMZ = si.cvParam(MS_m_z).valueAs<double>();
                if (scanInfo.precursorMZ == 0)
                    throw runtime_error("No precursor m/z for " + spectrum->id);

                scanInfo.precursorScanStartTime =
                    ms1ScanMap.get<nativeID>().find(scanInfo.precursorNativeID)->scanStartTime;

                // Only look at retention times of peptides identified in .idpDB
                // curIndex is the spectrum index, curIndex+1 is (usually) the scan number
                map<string, string>::const_iterator findItr = distinctModifiedPeptideByNativeID.find(spectrum->id);
                if (findItr != distinctModifiedPeptideByNativeID.end())
                {
                    scanInfo.identified = true;
                    scanInfo.distinctModifiedPeptide = findItr->second;
                    map<string, double>::iterator insertItr = firstScanTimeOfDistinctModifiedPeptide.insert(make_pair(findItr->second, scanInfo.scanStartTime)).first;
                    insertItr->second = min(insertItr->second, scanInfo.scanStartTime);
                }
                else
                { // this MS2 scan was not identified; we need this data for metrics MS2-4A/B/C/D
                    scanInfo.identified = false;
                    scanInfo.distinctModifiedPeptide = "";
                }
                ms2ScanMap.push_back(scanInfo);
            }

        } // finished cycling through all spectra

//        if (g_numWorkers == 1)
//            cout << endl;

        /*if (missingPrecursorIntensities)
            cerr << "Warning: " << missingPrecursorIntensities << " spectra are missing precursor trigger intensity; MS2 TIC will be used as a substitute." << endl;*/

        if (MS1Count == 0)
            throw runtime_error("no MS1 spectra found in \"" + sourceFilename + "\". Is the file incorrectly filtered?");

        if (MS2Count == 0)
            throw runtime_error("no MS2 spectra found in \"" + sourceFilename + "\". Is the file incorrectly filtered?");

        /*if (MS3OrGreaterCount)
            cerr << "Warning: " << MS3OrGreaterCount << " spectra have MS levels higher than 2 but will be ignored." << endl;*/

        XICWindowList pepWindow = GetMZRTWindows(idpDb,ms2ScanMap,sourceId, config);
        vector<RegDefinedPrecursorInfo> RegDefinedPrecursors;


//TODO: Give option for # if align the chromatogram peaks based on peptide retention time
        /*if(g_rtConfig->peakAlignment)
        {
            string outputFilepath = g_rtConfig->OutputFilepath;
            string regressionFilename=bfs::change_extension(bfs::path(outputFilepath) / bfs::path(sourceFilename).filename(), "-peptideScantimeRegression.tsv").string();
            ifstream file;

            try
            {
                file.open(regressionFilename.c_str()); // how to use a "string" variable
                if(!file)
                    throw(regressionFilename);//If the file is not found, this calls the "catch"
            }
            catch(string regressionFilename)//This catches the infile and aborts process
            {
                cout << "Fatal error: File not found."<<endl<<"Abort process."<<endl;
                exit(1);
            }

            if (file.is_open())
            {
                RegDefinedPrecursorInfo info;

                std::string   line;
                getline(file, line);
                while(getline(file, line))
                {
                    istringstream   ss(line);
                    string         sequence, mods;
                    double     scantime1,precursorMZ;
                    int  charge;
                    ss >> sequence>>scantime1>>charge>>precursorMZ>>mods;
                    info.peptide=sequence;
                    info.exactMZ=precursorMZ;
                    info.mods=mods;
                    info.charge=charge;
                    info.RegTime=scantime1;
                    info.scanTimeWindow=g_rtConfig->chromatogramRegressionTimeWindow(info.RegTime);
                    info.mzWindow=g_rtConfig->chromatogramMzWindow(info.exactMZ,charge);
                    info.chromatogram.id = "regression defined precursor " +info.peptide+" "+lexical_cast<string>(info.charge)+" "+info.mods+ "(id: m/z: "+lexical_cast<string>(round(info.exactMZ,3))+"; time: "+lexical_cast<string>(info.RegTime)+") ";
                    RegDefinedPrecursors.push_back(info);
                }
                file.close();
            }
            cout<<"number of regdefined precursors are "<<RegDefinedPrecursors.size()<<endl;
        }*/
        // Going through all spectra once more to get intensities/retention times to build chromatograms
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "\rReading " + lexical_cast<string>(spectrumList.size()) + " peaks...");
        for( size_t curIndex = 0; curIndex < spectrumList.size(); ++curIndex )
        {
//
//          if (curIndex+1==spectrumList.size() || !((curIndex+1)%100))
//          ITERATION_UPDATE(ilr, currentFile, totalFiles, "\r§Reading peaks: " + lexical_cast<string>(curIndex+1) + "/" + lexical_cast<string>(spectrumList.size()));

            SpectrumPtr spectrum = spectrumList.spectrum(curIndex, true);

            if (spectrum->cvParam(MS_MSn_spectrum).empty() && spectrum->cvParam(MS_MS1_spectrum).empty() )
                continue;

            CVParam spectrumMSLevel = spectrum->cvParam(MS_ms_level);
            if (spectrumMSLevel == CVID_Unknown)
                continue;
            // this time around we're only looking for MS1 spectra
            int msLevel = spectrumMSLevel.valueAs<int>();
            if (msLevel == 1)
            {
                Scan& scan = spectrum->scanList.scans[0];

                // all m/z and intensity data for a spectrum
                const vector<double>& mzV = spectrum->getMZArray()->data;
                const vector<double>& intensV = spectrum->getIntensityArray()->data;
                size_t arraySize = mzV.size();
                double curRT = scan.cvParam(MS_scan_start_time).timeInSeconds();

                accs::accumulator_set<double, accs::stats<accs::tag::min, accs::tag::max> > mzMinMax;
                mzMinMax = std::for_each(mzV.begin(), mzV.end(), mzMinMax);
                boost::icl::interval_set<double> spectrumMzRange(continuous_interval<double>::closed(accs::min(mzMinMax), accs::max(mzMinMax)));

                BOOST_FOREACH(const XICWindow& window, pepWindow)
                {
                    // if the MS1 retention time is not in the RT window constructed for this peptide, skip this window
                    if (!boost::icl::contains(hull(window.preRT), curRT))
                        continue;

                    // if the m/z window and the MS1 spectrum's m/z range do not overlap, skip this window
                    if (disjoint(window.preMZ, spectrumMzRange))
                        continue;

                    double sumIntensities = 0;
                    for (size_t iMZ = 0; iMZ < arraySize; ++iMZ)
                    {
                        // if this m/z is in the window, record its intensity and retention time
                        if (boost::icl::contains(window.preMZ, mzV[iMZ]))
                            sumIntensities += intensV[iMZ];
                    }

                    window.MS1Intensity.push_back(sumIntensities);
                    window.MS1RT.push_back(curRT);

                } // done searching through all unique peptide windows for this MS1 scan


                //loop through all regression defined precursors

                      BOOST_FOREACH(RegDefinedPrecursorInfo& info, RegDefinedPrecursors)
                {

                    if (!boost::icl::contains(info.scanTimeWindow, curRT))
                        continue;

                    // if the PSM's m/z window and the MS1 spectrum's m/z range do not overlap, skip this window
                    if (disjoint(info.mzWindow, spectrumMzRange))
                        continue;

                    double sumIntensities = 0;
                    for (size_t iMZ = 0; iMZ < arraySize; ++iMZ)
                    {
                        // if this m/z is in the window, record its intensity and retention time
                        if (boost::icl::contains(info.mzWindow, mzV[iMZ]))
                            sumIntensities += intensV[iMZ];
                    }

                    info.chromatogram.MS1Intensity.push_back(sumIntensities);
                    info.chromatogram.MS1RT.push_back(curRT);
                } // done with regression defined precursor scan

            }
            else if (msLevel == 2)
            {
                // calculate TIC manually if necessary
                MS2ScanInfo& scanInfo = const_cast<MS2ScanInfo&>(*ms2ScanMap.get<nativeID>().find(spectrum->id));
                if (scanInfo.precursorIntensity == 0)
                {
                    BOOST_FOREACH(const double& p, spectrum->getIntensityArray()->data)
                        scanInfo.precursorIntensity += p;
                }
            }
        } // end of spectra loop

        // Find peaks with Crawdad
        // cycle through all distinct matches, passing each one to crawdad
        size_t i = 0;
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "\rFinding distinct match peaks...");
        BOOST_FOREACH(const XICWindow& window, pepWindow)
        {
            ++i;
            if (window.MS1RT.empty())
            {
                /*cerr << "Warning: distinct match window for " << window.peptide
                     << " (id: " << window.PSMs[0].peptide
                     << "; m/z: " << window.preMZ
                     << "; time: " << window.preRT
                     << ") has no chromatogram data points!" << endl;*/
                continue;
            }

                if(window.MS1RT.size()<4){
            //cerr<<"Warning: the MS1 RT size is less than 4 :"<<window.peptide<<endl;
            continue;
            }

            // make chromatogram data points evenly spaced
            Interpolator interpolator(window.MS1RT, window.MS1Intensity);
            interpolator.resample(window.MS1RT, window.MS1Intensity);

            // eliminate negative signal
            BOOST_FOREACH(double& intensity, window.MS1Intensity)
                intensity = max(0.0, intensity);
            CrawdadPeakFinder crawdadPeakFinder;
            crawdadPeakFinder.SetChromatogram(window.MS1RT, window.MS1Intensity);

            vector<CrawdadPeakPtr> crawPeaks = crawdadPeakFinder.CalcPeaks();
            if (crawPeaks.size() == 0)
                continue;

            // if there are more than 2 PSMs, we interpolate a curve from their time/score pairs and
            // use the dot product between it and the interpolated SIC to pick the peak
            boost::optional<Interpolator> ms2Interpolator;
            vector<double> ms2Times, ms2Scores;
            if (window.PSMs.size() > 1)
            {
                // calculate the minimum time gap between PSMs
                double minDiff = window.PSMs[1].spectrum->scanStartTime - window.PSMs[0].spectrum->scanStartTime;
                for (size_t i=2; i < window.PSMs.size(); ++i)
                    minDiff = min(minDiff, window.PSMs[i].spectrum->scanStartTime - window.PSMs[i-1].spectrum->scanStartTime);

                // add zero scores before and after the "curve" using the minimum time gap
                ms2Times.push_back(window.PSMs.front().spectrum->scanStartTime - minDiff);
                ms2Scores.push_back(0);
                BOOST_FOREACH(const XICPeptideSpectrumMatch& psm, window.PSMs)
                {
                    ms2Times.push_back(psm.spectrum->scanStartTime);
                    ms2Scores.push_back(psm.score);
                }
                ms2Times.push_back(window.PSMs.back().spectrum->scanStartTime + minDiff);
                ms2Scores.push_back(0);

                ms2Interpolator = Interpolator(ms2Times, ms2Scores);
            }

            // find the peak with the highest sum of (PSM scores * interpolated SIC) within the peak;
            // if no IDs fall within peaks, find the peak closest to the best scoring id
            map<double, map<double, Peak> > peakByIntensityBySumOfProducts;
            BOOST_FOREACH(const CrawdadPeakPtr& crawPeak, crawPeaks)
            {
                double startTime = window.MS1RT[crawPeak->getStartIndex()];
                double endTime = window.MS1RT[crawPeak->getEndIndex()];
                double peakTime = window.MS1RT[crawPeak->getTimeIndex()];
                //double peakTime = startTime + (endTime-startTime)/2;

                // skip degenerate peaks
                if (crawPeak->getFwhm() == 0 || boost::math::isnan(crawPeak->getFwhm()) || startTime == peakTime || peakTime == endTime)
                    continue;

                // skip peaks which don't follow the raw data
                double rawPeakIntensity = window.MS1Intensity[crawPeak->getTimeIndex()];
                if (rawPeakIntensity < window.MS1Intensity[crawPeak->getStartIndex()] ||
                    rawPeakIntensity < window.MS1Intensity[crawPeak->getEndIndex()])
                    continue;

                // Crawdad Fwhm is in index units; we have to translate it back to time units
                double sampleRate = (endTime-startTime) / (crawPeak->getEndIndex()-crawPeak->getStartIndex());
                Peak peak(startTime, endTime, peakTime, crawPeak->getFwhm() * sampleRate, crawPeak->getHeight());
                window.peaks.insert(peak);

                if (!window.bestPeak || fabs((window.bestPeak->peakTime - window.bestScoreScanStartTime) / window.bestPeak->fwhm) >
                                        fabs((peakTime - window.bestScoreScanStartTime) / peak.fwhm))
                    window.bestPeak = peak;

                // calculate sum of products between PSM score and interpolated SIC over 4 standard deviations
                double wideStartTime = peakTime - peak.fwhm * 4 / 2.35482;
                double wideEndTime = peakTime + peak.fwhm  * 4 / 2.35482;
                double sumOfProducts = 0;
                if (ms2Interpolator)
                {
                    size_t wideStartIndex = boost::lower_bound(window.MS1RT, wideStartTime) - window.MS1RT.begin();
                    size_t wideEndIndex = boost::upper_bound(window.MS1RT, wideEndTime) - window.MS1RT.begin();
                    for (size_t i=wideStartIndex; i < wideEndIndex; ++i)
                        sumOfProducts += window.MS1Intensity[i] * ms2Interpolator->interpolate(ms2Times, ms2Scores, window.MS1RT[i]);
                }
                else
                {
                    BOOST_FOREACH(const XICPeptideSpectrumMatch& psm, window.PSMs)
                    {
                        if (wideStartTime <= psm.spectrum->scanStartTime && psm.spectrum->scanStartTime <= wideEndTime)
                            sumOfProducts += psm.score * interpolator.interpolate(window.MS1RT, window.MS1Intensity, psm.spectrum->scanStartTime);
                    }
                }

                if (sumOfProducts > 0)
                    peakByIntensityBySumOfProducts[sumOfProducts][peak.intensity] = peak;
            }
            if (!peakByIntensityBySumOfProducts.empty())
                window.bestPeak = peakByIntensityBySumOfProducts.rbegin()->second.rbegin()->second;

            /*if (!window.bestPeak)
            {
                cerr << "Warning: unable to select the best Crawdad peak for distinct match! " << window.peptide
                     << " (id: " << window.PSMs[0].peptide
                     << "; m/z: " << window.preMZ
                     << "; time: " << window.preRT
                     << ")" << endl;
            }
            else
                cout << "\n" << firstMS2.nativeID << "\t" << window.peptide << "\t" << window.bestPeak->intensity << "\t" << window.bestPeak->peakTime << "\t" << firstMS2.precursorIntensity << "\t" << (window.bestPeak->intensity / firstMS2.precursorIntensity);*/
        }
        i = 0;
        //cycle through all regression defined precursors, passing each one to crawdad
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "\rFinding regression defined peptide peaks...");
            BOOST_FOREACH(RegDefinedPrecursorInfo& info, RegDefinedPrecursors)
        {

            ++i;
            LocalChromatogram& lc = info.chromatogram;
            if (lc.MS1RT.empty())
            {
                /*cerr << "Warning: regression defined precursor m/z " << info.exactMZ
                     << " (m/z: " << info.mzWindow
                     << "; time: " << info.scanTimeWindow
                     << ") has no chromatogram data points!" << endl;*/
                continue;
            }

                if(lc.MS1RT.size()<4){
            //cerr<<"Warning: the MS1 RT size is less than 4: "<<info.exactMZ<<endl;
            continue;
            }

             //try{

            // make chromatogram data points evenly spaced
            Interpolator(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity).resample(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity);


            // eliminate negative signal
            BOOST_FOREACH(double& intensity, info.chromatogram.MS1Intensity)
                intensity = max(0.0, intensity);

            CrawdadPeakFinder crawdadPeakFinder;
            crawdadPeakFinder.SetChromatogram(lc.MS1RT, lc.MS1Intensity);
            info.baselineIntensity= crawdadPeakFinder.getBaselineIntensity();

            vector<CrawdadPeakPtr> crawPeaks = crawdadPeakFinder.CalcPeaks();
             if (crawPeaks.size() == 0)
                continue;



            BOOST_FOREACH(const CrawdadPeakPtr& crawPeak, crawPeaks)
            {

                double startTime = lc.MS1RT[crawPeak->getStartIndex()];
                double endTime = lc.MS1RT[crawPeak->getEndIndex()];
                double peakTime = lc.MS1RT[crawPeak->getTimeIndex()];
                //double peakTime = startTime + (endTime-startTime)/2;

                // skip degenerate peaks
                if (crawPeak->getFwhm() == 0 || boost::math::isnan(crawPeak->getFwhm()) || startTime == peakTime || peakTime == endTime)
                    continue;

                // skip peaks which don't follow the raw data
                double rawPeakIntensity = lc.MS1Intensity[crawPeak->getTimeIndex()];
                if (rawPeakIntensity < lc.MS1Intensity[crawPeak->getStartIndex()] ||
                    rawPeakIntensity < lc.MS1Intensity[crawPeak->getEndIndex()])
                    continue;

                // Crawdad Fwhm is in index units; we have to translate it back to time units
                double sampleRate = (endTime-startTime) / (crawPeak->getEndIndex()-crawPeak->getStartIndex());
                Peak peak(startTime, endTime, peakTime, crawPeak->getFwhm() * sampleRate, crawPeak->getHeight());
                lc.peaks.insert(peak);

                //if bestPeak is still null  (i.e. if statement is executing for the first time) or the current peak is closer to the regression time than the best peak, set the current peak as the best peak
                if (!lc.bestPeak || fabs(peakTime - info.RegTime) < fabs(lc.bestPeak->peakTime - info.RegTime))
                    lc.bestPeak = peak;

            }
             /*}


                catch(exception& e)
                {
                continue;
                }*/

        }
        // Write chromatograms for visualization of data
        return writeChromatograms(idpDBFilePath, pepWindow,RegDefinedPrecursors, ilr, currentFile, totalFiles);
    }
    catch (exception& e)
    {
        throw runtime_error(string("[EmbedMS1ForFile] ") + e.what());
    }
    return 0;
}

} // namespace XIC
END_IDPICKER_NAMESPACE
