//
// $Id: Types.cs 1599 2009-12-04 01:35:39Z brendanx $
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

using System;
using System.Collections.Generic;
using pwiz.CLI.proteome;
using System.Drawing;
using System.Windows.Forms;

namespace IonMatcher
{
    public class ScoringUtils
    {
        /// <summary>
        /// A peak in a mass spectrum.
        /// </summary>
        public class Peak : IComparable<Peak>
        {
            public double mz;
            public double rankOrIntensity;

            public Peak(double mass, double rnkOrIntens)
            {
                mz = mass;
                rankOrIntensity = rnkOrIntens;
            }

            public Peak(Peak pk)
            {
                mz = pk.mz;
                rankOrIntensity = pk.rankOrIntensity;
            }

            public Peak(double mass)
            {
                mz = mass;
                rankOrIntensity = -1;
            }

            public int CompareTo(Peak rhs)
            {
                if (mz < rhs.mz)
                    return -1;
                else if (mz > rhs.mz)
                    return 1;
                return 0;
            }
        }

        // Comparators for Peaks in mass spectrum
        public enum SortOrder { ASCENDING, DESCENDING };
        public class MassComparer : IComparer<Peak>
        {
            SortOrder sortOrder;
            public MassComparer(SortOrder order)
            {
                this.sortOrder = order;
            }

            public int Compare(Peak a, Peak b)
            {
                if (a.mz < b.mz)
                    return sortOrder == SortOrder.ASCENDING ? -1 : 1;
                else if (a.mz > b.mz)
                    return sortOrder == SortOrder.ASCENDING ? 1 : -1; ;
                return 0;
            }
        }

        public class IntensityComparer : IComparer<Peak>
        {

            SortOrder sortOrder;
            public IntensityComparer(SortOrder order)
            {
                this.sortOrder = order;
            }

            public int Compare(Peak a, Peak b)
            {
                if (a.rankOrIntensity < b.rankOrIntensity)
                    return sortOrder == SortOrder.ASCENDING ? -1 : 1;
                else if (a.rankOrIntensity > b.rankOrIntensity)
                    return sortOrder == SortOrder.ASCENDING ? 1 : -1;
                return 0;
            }
        }
        // End comparators for Peak

        /// <summary>
        /// This function takes top N intense peaks from each spectrum
        /// </summary>
        /// <param name="peaks">Spectrum peaks</param>
        /// <param name="numPeaks">Number of peaks to be retained</param>
        /// <returns></returns>
        public static List<Peak> filterByPeakCount(List<Peak> peaks, int numPeaks)
        {
            // Sort the peaks by decreasing order of intensity and take numPeaks
            IntensityComparer intensitySorter = new IntensityComparer(SortOrder.DESCENDING);
            peaks.Sort(intensitySorter);
            List<Peak> finalList = new List<Peak>();
            foreach (var peak in peaks)
            {
                if (numPeaks == 0)
                    break;
                --numPeaks;
                finalList.Add(peak);
            }
            MassComparer mzComparer = new MassComparer(SortOrder.ASCENDING);
            finalList.Sort(mzComparer);
            return finalList;
        }

        /// <summary>
        /// This function ranks peaks based on the increasing order of intenstiy.
        /// </summary>
        /// <param name="peaks">Peaks in spectrum</param>
        /// <returns>Ranked peaks</returns>
        public static Set<Peak> rankPeaks(List<Peak> peaks)
        {
            Stack<Peak> rankedPeaks = new Stack<Peak>();
            IntensityComparer intensitySorter = new IntensityComparer(SortOrder.ASCENDING);
            peaks.Sort(intensitySorter);
            int peakCount = 1;
            foreach (var peak in peaks)
            {
                Peak newPeak = new Peak(peak);
                newPeak.rankOrIntensity = peakCount;
                if (rankedPeaks.Count > 0)
                {
                    Peak lastPeak = rankedPeaks.Peek();
                    if (lastPeak.rankOrIntensity == newPeak.rankOrIntensity)
                        newPeak.rankOrIntensity = lastPeak.rankOrIntensity;
                }
                rankedPeaks.Push(newPeak);
                ++peakCount;
            }
            Set<Peak> retPeaks = new Set<Peak>();
            foreach (var peak in rankedPeaks)
                retPeaks.Add(peak);
            return retPeaks;
        }

        /// <summary>
        /// This function takes top N intense peaks to meet the target TIC.
        /// </summary>
        /// <param name="peaks">Peaks in the spectrum</param>
        /// <param name="percentTIC">Percentage of total ion current to be retained</param>
        /// <returns></returns>
        public static List<Peak> filterByPercentTIC(List<Peak> peaks, double percentTIC)
        {
            // Compute the total ion current in the spectrum
            double totalTIC = 0.0;
            foreach (var peak in peaks)
                totalTIC += peak.rankOrIntensity;

            // Sort the peaks by descending order of intensity and take as many peaks as
            // possible to meet the target TIC.
            IntensityComparer intensitySorter = new IntensityComparer(SortOrder.DESCENDING);
            peaks.Sort(intensitySorter);
            List<Peak> finalList = new List<Peak>();
            double TICSoFar = 0.0;
            foreach (var peak in peaks)
            {
                TICSoFar += peak.rankOrIntensity;
                if ((TICSoFar / totalTIC) >= percentTIC)
                    break;
                finalList.Add(peak);
            }
            MassComparer mzComparer = new MassComparer(SortOrder.ASCENDING);
            finalList.Sort(mzComparer);
            return finalList;
        }

        /// <summary>
        /// This function takes a spectrum and wipes out precursor and all its
        /// neutral losses from the spectrum.
        /// </summary>
        /// <param name="precursorMZ">m/z of the precursor</param>
        /// <param name="rawPeaks">Cleaned spectrum</param>
        public static void erasePrecursorIons(double precursorMZ, ref List<Peak> rawPeaks)
        {
            // Get the masses of the precursor and associated neutral loss peaks
            double NH3Loss = precursorMZ - 17.03;
            double doubleNH3Loss = NH3Loss - 17.03;
            double waterLoss = precursorMZ - 18.01;
            double doubleWaterLoss = waterLoss - 18.01;
            // Accept spectrum peaks that are not with in +/- 1 m/z 
            // range of the precursor ions
            List<Peak> neutralLossPeaks = new List<Peak>();
            foreach (var peak in rawPeaks)
            {
                double parentError = Math.Abs(peak.mz - precursorMZ);
                double NH3LossError = Math.Abs(peak.mz - NH3Loss);
                double NH3DoubleLossError = Math.Abs(peak.mz - doubleNH3Loss);
                double waterLossError = Math.Abs(peak.mz - waterLoss);
                double doubleWaterLossError = Math.Abs(peak.mz - doubleWaterLoss);
                if (NH3LossError <= 1.0 || NH3DoubleLossError <= 1.0 || waterLossError <= 1.0 || doubleWaterLossError <= 1.0 || parentError <= 1.0)
                    neutralLossPeaks.Add(peak);
            }
            foreach (var lossPeak in neutralLossPeaks)
                rawPeaks.Remove(lossPeak);
        }

        /// <summary>
        /// This function takes a spectrum and processes it for computing Sequest XCorr.
        /// </summary>
        /// <param name="originalPeaks">Peaks in the spectrum</param>
        /// <param name="precursorMH">Mass of the precursor</param>
        /// <returns>An intensity array of the processed spectrum</returns>
        public static double[] processSpectrumForXCorr(ref List<Peak> originalPeaks, double precursorMH)
        {
            // Sort the peaks by mass
            MassComparer massComp = new MassComparer(SortOrder.ASCENDING);
            originalPeaks.Sort(massComp);
            // Get the number of bins and bin width for the processed peak array
            double binWidth = 1.0005079;
            int maxBins;
            double massCutOff = precursorMH + 50;
            if (massCutOff > 512)
                maxBins = (int) Math.Ceiling(massCutOff / 1024) * 1024;
            else
                maxBins = 512;
            
            double maxPeakMass = originalPeaks[originalPeaks.Count - 1].mz;
            if (maxPeakMass > massCutOff)
            {
                int index = originalPeaks.Count - 2;
                for (; index >= 0 && maxPeakMass > massCutOff; --index)
                    maxPeakMass = originalPeaks[index].mz;
            }
            int numberOfRegions = 10;

            // Square root the intensity of the spectrum
            foreach (var peak in originalPeaks)
                peak.rankOrIntensity = Math.Sqrt(peak.rankOrIntensity);

            // Section the original peak array in 10 bins and find the
            // base peak in each bin.
            double[] maxPeakIntensityInRegions = new double[numberOfRegions];
            // Zero out the base peak array
            for (int index = 0; index < maxPeakIntensityInRegions.Length; ++index)
                maxPeakIntensityInRegions[index] = 0;
            // Determine the base peak in each region
            int regionSelector = (int)maxPeakMass / numberOfRegions;
            foreach (var peak in originalPeaks)
            {
                int peakBin = (int)peak.mz / regionSelector;
                double peakIntensity = peak.rankOrIntensity;
                if (peakBin < 10 && maxPeakIntensityInRegions[peakBin] < peakIntensity)
                    maxPeakIntensityInRegions[peakBin] = peakIntensity;
            }

            // Normalize peaks in each region from 0 to 50. 
            // Use base peak in each region for normalization. 
            double[] processedPeaks = new double[maxBins];
            foreach (var peak in originalPeaks)
            {
                int mzLocation = (int)(peak.mz / binWidth + 0.5);
                int mzBin = mzLocation / regionSelector;
                if (mzBin + 1 > numberOfRegions)
                    continue;
                double maxBinIntensity = maxPeakIntensityInRegions[mzBin];
                processedPeaks[mzLocation] = (peak.rankOrIntensity / maxBinIntensity) * 50;
            }

            // Compute the cumulative spectrum
            for (int index = 0; index < processedPeaks.Length; ++index)
                for (int subIndex = index - 75; subIndex <= index + 75; ++subIndex)
                {
                    if (subIndex <= 0 || subIndex >= processedPeaks.Length)
                        continue;
                    processedPeaks[index] -= (processedPeaks[subIndex] / 151);
                }

            return processedPeaks;
        }

        /// <summary>
        /// This function takes a spectrum and a peptide and computes the 
        /// sequest style cross-correlation.
        /// </summary>
        /// <param name="peaks">Unprocessed experimental spectrum </param>
        /// <param name="peptide">Peptide sequence for correlation</param>
        /// <param name="precursorMH">Mass of the peptide precursor</param>
        /// <returns>An XCorr</returns>
        public static double computeXCorr(List<Peak> peaks, Peptide peptide, double precursorMH)
        {
            // Prepare the spectrum for XCorr
            double[] processedSpectrum = processSpectrumForXCorr(ref peaks, precursorMH);
            // Generate the theoretical spectrum for the candidate.
            Map<double, int> frags = new Map<double, int>();
            Map<double, char> fragTypes = new Map<double, char>();
            calculateSequenceIons(peptide, 2, ref frags, ref fragTypes);
            double[] peptideTheoreticalSpectrum = getTheoreticalSpectrumForXCorr(frags, fragTypes, processedSpectrum.Length);
            // Compute and return the XCorr
            return crossCorrelation(processedSpectrum, peptideTheoreticalSpectrum);
        }

        ///// <summary>
        ///// This function takes an experimental spectrum, two peptide candidates
        ///// and a delta score threshold. It checks to see if XCorrs of the two 
        ///// candidates differ more than the specified delta score threshold.
        ///// </summary>
        ///// <param name="peaks">Unprocessed experimental spectrum</param>
        ///// <param name="primary">First peptide</param>
        ///// <param name="secondary">Second peptide</param>
        ///// <param name="precursorMH">Mass of the peptide precursor</param>
        ///// <param name="threshold">Delta score threshold</param>
        ///// <returns>Status of whether the peptides score differently or not</returns>
        //public static PTMDigger.AttestationStatus compareXCorrs(List<Peak> peaks, Peptide primary, Peptide secondary, double precursorMH, double threshold)
        //{
        //    PTMDigger.AttestationStatus status = PTMDigger.AttestationStatus.UNKNOWN;
        //    // Prepare the spectrum for computing XCorr
        //    double[] processedSpectrum = processSpectrumForXCorr(ref peaks, precursorMH);
        //    // Predict spectra for both sequences.
        //    Map<double, int> frags = new Map<double, int>();
        //    Map<double, char> fragTypes = new Map<double, char>();
        //    calculateSequenceIons(primary, 2, ref frags, ref fragTypes);
        //    double[] primaryTheoreticalSpectrum = getTheoreticalSpectrumForXCorr(frags, fragTypes, processedSpectrum.Length);
        //    frags.Clear();
        //    fragTypes.Clear();
        //    calculateSequenceIons(secondary, 2, ref frags, ref fragTypes);
        //    double[] secondaryTheoreticalSpectrum = getTheoreticalSpectrumForXCorr(frags, fragTypes, processedSpectrum.Length);
        //    // Compute XCorrs for both peptides, and compare
        //    double primaryXCorr = crossCorrelation(processedSpectrum, primaryTheoreticalSpectrum);
        //    double secondaryXCorr = crossCorrelation(processedSpectrum, secondaryTheoreticalSpectrum);
        //    double deltaCN = Math.Abs(primaryXCorr-secondaryXCorr)/Math.Max(primaryXCorr,secondaryXCorr);
        //    if(deltaCN <= threshold)
        //        status = PTMDigger.AttestationStatus.AMBIGUOUS;
        //    else 
        //    {
        //        if(primaryXCorr > secondaryXCorr)
        //            status = PTMDigger.AttestationStatus.PRIMARY_RESULT_IS_BETTER;
        //        else
        //            status = PTMDigger.AttestationStatus.SECONDARY_RESULT_IS_BETTER;
        //    }
        //    return status;
        //}

        ///// <summary>
        ///// This function accepts an experimental spectrum, two peptides, 
        ///// and a delta TIC threshold. It checks to see if both peptides
        ///// explain similar amount of intensity in the spectrum or not.
        ///// </summary>
        ///// <param name="peaks">Unprocessed spectrum</param>
        ///// <param name="primary">First peptide</param>
        ///// <param name="secondary">Second peptide</param>
        ///// <param name="threshold">Delta TIC threshold</param>
        ///// <returns>Whether the two peptides explain different amounts of the TIC</returns>
        //public static PTMDigger.AttestationStatus compareMatchedTIC(List<Peak> peaks, Peptide primary, Peptide secondary, double threshold)
        //{
        //    PTMDigger.AttestationStatus status = PTMDigger.AttestationStatus.UNKNOWN;
        //    // Filter the peaks and rank them based on increasing 
        //    // order of intensity: i.e. intense peaks get higher ranks 
        //    peaks = filterByPeakCount(peaks, 100);
        //    Set<Peak> rankedPeaks = new Set<Peak>(peaks);

        //    // Predict the fragment ions for both peptides
        //    Set<double> primaryMatchFragMasses = new Set<double>();
        //    calculateSequenceIons(primary, 2, ref primaryMatchFragMasses);
        //    Set<double> secondaryMatchFragMasses = new Set<double>();
        //    calculateSequenceIons(secondary, 2, ref secondaryMatchFragMasses);
        //    // Compute the matched TIC for both peptides
        //    double primarySeqMatchedIntens = 0.0;
        //    foreach (var peak in primaryMatchFragMasses)
        //    {
        //        Peak match = findNear(rankedPeaks, peak, 0.5);
        //        if (match != null)
        //            primarySeqMatchedIntens += match.rankOrIntensity;
        //    }
        //    double secondarySeqMatchedIntens = 0;
        //    foreach (var peak in secondaryMatchFragMasses)
        //    {
        //        Peak match = findNear(rankedPeaks, peak, 0.5);
        //        if (match != null)
        //            secondarySeqMatchedIntens += match.rankOrIntensity;
        //    }
        //    // Compare the percent of the matched TICs
        //    double TIC = 0.0;
        //    foreach (var peak in rankedPeaks)
        //        TIC += peak.rankOrIntensity;
        //    double percentTICDiff = Math.Abs(primarySeqMatchedIntens - secondarySeqMatchedIntens) / TIC;
        //    if (percentTICDiff <= threshold)
        //        status = PTMDigger.AttestationStatus.AMBIGUOUS;
        //    else
        //    {
        //        if (primarySeqMatchedIntens > secondarySeqMatchedIntens)
        //            status = PTMDigger.AttestationStatus.PRIMARY_RESULT_IS_BETTER;
        //        else
        //            status = PTMDigger.AttestationStatus.SECONDARY_RESULT_IS_BETTER;
        //    }
        //    return status;
        //}

        /// <summary>
        /// Poor mans version of an input dialog. MicroSoft should really put in an input
        /// dialog for C# framework. 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="promptText"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }

        /// <summary>
        ///  This function takes a set of peaks, a lookup mass, and 
        ///  finds the closest peak with in a certain tolerance. If 
        ///  multiple peaks exists with in the window, most intense peak
        ///  is selected
        /// </summary>
        /// <param name="peaks">A spectrum</param>
        /// <param name="mz">Look up m/z</param>
        /// <param name="tolerance">Mass tolerance for look-up</param>
        /// <returns>Peak if found</returns>
        public static Peak findNear(Set<Peak> peaks, double mz, double tolerance)
        {
            Set<Peak>.Enumerator cur, min, max;

            min = peaks.LowerBound(new Peak(mz - tolerance));
            max = peaks.LowerBound(new Peak(mz + tolerance));
            if (!min.IsValid && !max.IsValid)
                return null;
            if (!min.IsValid && max.IsValid)
                return max.Current;
            if (min.IsValid && !max.IsValid)
                return min.Current;
            if (min.Current == max.Current)
                return null;

            // If we found multiple matching peaks, 
            // return the peak with best intensity.
            Peak best = min.Current;
            double bestIntensityOrRank = best.rankOrIntensity;
            for (cur = min; cur.Current != max.Current; cur.MoveNext())
            {
                double curRank = cur.Current.rankOrIntensity;
                if (curRank > bestIntensityOrRank)
                {
                    bestIntensityOrRank = curRank;
                    best = cur.Current;
                }
            }
            return best;
        }

        /// <summary>
        /// This function takes a peptide and predicts the b/y sequence ions associated with it.
        /// </summary>
        /// <param name="peptide"></param>
        /// <param name="maxIonCharge"></param>
        /// <param name="fragMasses"></param>
        public static void calculateSequenceIons(Peptide peptide, int maxIonCharge, ref Set<double> fragMasses)
        {
            Fragmentation fragmentation = peptide.fragmentation(true, true);
            for (int i = 1; i <= maxIonCharge; ++i)
            {
                for (int c = 0; c < peptide.sequence.Length; ++c)
                {
                    int nLength = c;
                    int cLength = peptide.sequence.Length - c;
                    if (nLength > 0)
                        fragMasses.Add(fragmentation.b(nLength, i));
                    if (cLength > 0)
                        fragMasses.Add(fragmentation.y(cLength, i));
                }
            }
        }

        /// <summary>
        /// This function takes a processed experimental spectrum, 
        /// and a theoretical spectrum. Both these spectra must have
        /// been prepared for computing the XCorr.
        /// </summary>
        /// <param name="observed"></param>
        /// <param name="theoretical"></param>
        /// <returns></returns>
        public static double crossCorrelation(double[] observed, double[] theoretical)
        {
            double score = 0.0;
            for (int index = 0; index < observed.Length; ++index)
                score += observed[index] * theoretical[index];

            return score / 1e4;
        }

        /// <summary>
        /// This function takes a set of fragment ions and generates 
        /// a theoretical spectrum that can be used for computing XCorr.
        /// </summary>
        /// <param name="frags">Mass and charge state of fragments</param>
        /// <param name="fragTypes">Mass and the type of fragments</param>
        /// <param name="length">Total length of the theoretical spectrum</param>
        /// <returns></returns>
        public static double[] getTheoreticalSpectrumForXCorr(Map<double, int> frags, Map<double, char> fragTypes, int length)
        {
            double[] theoreticalSpectrum = new double[length];
            double binWidth = 1.0007079;
            foreach (var fragment in fragTypes)
            {
                // Get the mass, charge, and type of the fragment.
                double fragMass = fragment.Key;
                char fragType = fragment.Value;
                int fragCharge = frags[fragMass];

                // Determine its corresponding bin
                int bin = (int)(fragMass / binWidth + 0.5);
                if (bin >= length)
                    continue;
                // Assign an intensity of 50 to b and y ions. 
                // Also assign an intensity of 25 to bins 
                // bordering the b and y ions. Neutral losses
                // are assigned an intensity of 10.
                if (fragType == 'b' || fragType == 'y')
                {
                    theoreticalSpectrum[bin] = 50;
                    int prevBin = bin - 1;
                    int nextBin = bin + 1;
                    if (prevBin >= 0 && prevBin < length)
                        theoreticalSpectrum[prevBin] = 25;
                    if (nextBin >= 0 && nextBin < length)
                        theoreticalSpectrum[nextBin] = 25;

                    int NH3LossIndex = (int)((fragMass - (17.02655 / fragCharge)) / binWidth + 0.5);
                    if (NH3LossIndex >= 0 && NH3LossIndex < length)
                        theoreticalSpectrum[NH3LossIndex] = 10;
                    if (fragType == 'b')
                    {
                        int H20LossIndex = (int)((fragMass - (18.01056 / fragCharge)) / binWidth + 0.5);
                        if (H20LossIndex >= 0 && H20LossIndex < length)
                            theoreticalSpectrum[H20LossIndex] = 10;
                    }
                }
                else if (fragType == 'a')
                    theoreticalSpectrum[bin] = 10;

            }
            return theoreticalSpectrum;
        }

        /// <summary>
        /// This function computes the b and y fragments needed 
        /// for the computation of XCorr.
        /// </summary>
        /// <param name="peptide">A peptide sequence</param>
        /// <param name="maxIonCharge">Max charge state for a fragment</param>
        /// <param name="frags">A reference map to hold the fragment masses and their charges states</param>
        /// <param name="fragTypes">A reference map to hold the fragment masses and their ion types</param>
        public static void calculateSequenceIons(Peptide peptide, int maxIonCharge, ref Map<double, int> frags, ref Map<double, char> fragTypes)
        {
            Fragmentation fragmentation = peptide.fragmentation(true, true);
            for (int i = 1; i <= maxIonCharge; ++i)
            {
                for (int c = 0; c < peptide.sequence.Length; ++c)
                {
                    int nLength = c;
                    int cLength = peptide.sequence.Length - c;
                    if (nLength > 0)
                    {
                        double mass = fragmentation.b(nLength, i);
                        frags.Insert(new MutableKeyValuePair<double, int>(mass, i));
                        fragTypes.Insert(new MutableKeyValuePair<double, char>(mass, 'b'));
                    }
                    if (cLength > 0)
                    {
                        double mass = fragmentation.y(cLength, i);
                        frags.Insert(new MutableKeyValuePair<double, int>(mass, i));
                        fragTypes.Insert(new MutableKeyValuePair<double, char>(mass, 'y'));
                    }
                }
            }
        }
    }
}
