package edu.uw.gs.skyline.cloudapi.chromatogramgenerator;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromExtractor;

import java.util.Arrays;

/**
 * Holds an intensity value, and a relative mass error.
 */
public class ChromatogramPointValue {
    private double intensity;
    private double relativeMassError;

    public ChromatogramPointValue(double intensity, double relativeMassError) {
        this.intensity = intensity;
        this.relativeMassError = relativeMassError;
    }

    /**
     * Returns the intensity of the chromatogram point.
     * For {@link ChromExtractor#SUMMED}, this would be the total intensity for each mz in the chromatogram extraction window.
     * For {@link ChromExtractor#BASE_PEAK}, this would be the highest intensity in the chromatogram extraction window.
     */
    public double getIntensity() {
        return intensity;
    }

    /**
     * Returns the relative mass error.
     * The relative mass error is calculated from and normalized to the center of the chromatogram extraction window.
     * In other places in the code, this relative mass error gets multiplied by a million to get the mass error ppm.
     */
    public double getRelativeMassError() {
        return relativeMassError;
    }

    /**
     * Calculate the intensity and mass error for a given targetMz, and extraction width
     * @param mzArray array of M/Zs in the spectrum
     * @param intensityArray array of intensities in the spectrum
     * @param targetMz center of the mz window for chromatogram extraction.  If targetMz is zero, then the entire
     *                 spectrum is used and {@paramref windowWidth} is ignored
     * @param extractor whether to sum all of the intensities in the window, or use only the highest intensity
     * @param windowWidth mz width for extracting the chromatogram
     * @return a ChromatogramPointValue with the calculated intensity and relative mass error.
     */
    public static ChromatogramPointValue calculate(double[] mzArray, double[] intensityArray, double targetMz, ChromExtractor extractor, double windowWidth) {
        int iStart;
        double endFilter;
        if (0 != targetMz) {
            iStart = Arrays.binarySearch(mzArray, targetMz - windowWidth / 2);
            if (iStart < 0) {
                iStart = ~iStart;
            }
            endFilter = targetMz + windowWidth / 2;
        } else {
            iStart = 0;
            endFilter = Double.MAX_VALUE;
        }
        double totalIntensity = 0;
        double meanError = 0;
        for (int iNext = iStart; iNext < mzArray.length && mzArray[iNext] < endFilter; iNext ++) {
            double mz = mzArray[iNext];
            double intensity = intensityArray[iNext];
            if (extractor == ChromExtractor.SUMMED) {
                totalIntensity += intensity;
            } else if (intensity > totalIntensity) {
                totalIntensity = intensity;
                meanError = 0;
            }
            if (extractor == ChromExtractor.SUMMED || meanError == 0) {
                if (totalIntensity > 0) {
                    double deltaPeak = mz - targetMz;
                    meanError += (deltaPeak - meanError) * intensity / totalIntensity;
                }
            }
        }
        double relativeMassError;
        if (targetMz == 0) {
            relativeMassError = 0;
        } else {
            relativeMassError = meanError / targetMz;
        }
        return new ChromatogramPointValue(totalIntensity, relativeMassError);
    }
}
