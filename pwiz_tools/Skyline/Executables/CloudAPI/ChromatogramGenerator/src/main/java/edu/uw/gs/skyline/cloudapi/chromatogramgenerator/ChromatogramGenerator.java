package edu.uw.gs.skyline.cloudapi.chromatogramgenerator;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromatogramRequestDocument;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.Ms2FullScanAcquisitionMethod;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsDataFile;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsPrecursor;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsSpectrum;

import javax.xml.bind.JAXBContext;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.*;

/**
 * Created by nicksh on 3/5/14.
 */
public class ChromatogramGenerator {
    private final ChromatogramRequestDocument chromatogramRequestDocument;
    private final List<ChromatogramGroupProcessor> filterPairs;
    public ChromatogramGenerator(ChromatogramRequestDocument chromatogramRequestDocument) {
        this.chromatogramRequestDocument = chromatogramRequestDocument;
        List<ChromatogramGroupProcessor> filterPairs = new ArrayList<ChromatogramGroupProcessor>();
        for (ChromatogramRequestDocument.ChromatogramGroup precursor : chromatogramRequestDocument.getChromatogramGroup()) {
            filterPairs.add(new ChromatogramGroupProcessor(precursor));
        }
        this.filterPairs = Collections.unmodifiableList(filterPairs);
    }
    public boolean containsTime(double time) {
        if (null != chromatogramRequestDocument.getMinTime() && chromatogramRequestDocument.getMinTime() > time) {
            return false;
        }
        if (null != chromatogramRequestDocument.getMaxTime() && chromatogramRequestDocument.getMaxTime() > time) {
            return false;
        }
        return true;
    }

    private Collection<IsolationWindowFilter> getIsolationWindows(MsPrecursor[] precursors) {
        List<IsolationWindowFilter> isolationWindows = new ArrayList<IsolationWindowFilter>();
        for (MsPrecursor msPrecursor : precursors) {
            isolationWindows.add(new IsolationWindowFilter(msPrecursor.getIsolationWindowTargetMz(), msPrecursor.getIsolationWidth()));
        }
        if (isolationWindows.size() == 0) {
            isolationWindows.add(new IsolationWindowFilter(null, null));
        }
        return isolationWindows;
    }

    private Collection<ChromatogramGroupProcessor> findGroupsForMs2IsolationWindow(IsolationWindowFilter isoWin, Ms2FullScanAcquisitionMethod fullScanAcquisitionMethod, boolean ignoreIsolationScheme) {
        List<ChromatogramGroupProcessor> result = new ArrayList<ChromatogramGroupProcessor>();
        if (null == isoWin.isolationMz) {
            return result;
        }
        if (fullScanAcquisitionMethod == Ms2FullScanAcquisitionMethod.DIA) {
            if (!ignoreIsolationScheme) {
                isoWin = CalcDiaIsolationValues(isoWin);
            }
            if (null == isoWin.isolationWidth) {
                return result; // empty
            }
            // For multiple case, find the first possible value, and iterate until
            // no longer matching or the end of the array is encountered
            int iFilter = indexOfFilter(isoWin.isolationMz, isoWin.isolationWidth);
            if (iFilter != -1) {
                while (iFilter < filterPairs.size() && CompareMz(isoWin.isolationMz, filterPairs.get(iFilter).getPrecursorMz(), isoWin.isolationWidth) == 0) {
                    result.add(filterPairs.get(iFilter++));
                }
            }
        } else {
            // For single (Targeted) case, review all possible matches for the one closest to the
            // desired precursor m/z value.
            // per "Issue 263: Too strict about choosing only one precursor for every MS/MS scan in Targeted MS/MS",
            // if more than one match at this m/z value return a list

            double minMzDelta = Double.MAX_VALUE;
            double mzDeltaEpsilon = Math.min(chromatogramRequestDocument.getMzMatchTolerance(), .0001);

            // Isolation width for single is based on the instrument m/z match tolerance
            isoWin = new IsolationWindowFilter(isoWin.isolationMz, chromatogramRequestDocument.getMzMatchTolerance() * 2);

            for (ChromatogramGroupProcessor filterPair : findGroupsForMs2IsolationWindow(isoWin, Ms2FullScanAcquisitionMethod.DIA, true))
            {
                double mzDelta = Math.abs(isoWin.isolationMz - filterPair.getPrecursorMz());
                if (mzDelta < minMzDelta) // new best match
                {
                    minMzDelta = mzDelta;
                    // are any existing matches no longer within epsilion of new best match?
                    for (int n = result.size(); n-- > 0; )
                    {
                        if ((Math.abs(isoWin.isolationMz - result.get(n).getPrecursorMz()) - minMzDelta) > mzDeltaEpsilon)
                        {
                            result.remove(n);  // no longer a match by our new standard
                        }
                    }
                    result.add(filterPair);
                }
                else if ((mzDelta - minMzDelta) <= mzDeltaEpsilon)
                {
                    result.add(filterPair);  // not the best, but close to it
                }
            }
        }
        return result;
    }

    private int indexOfFilter(double precursorMz, double window)
    {
        return indexOfFilter(precursorMz, window, 0, filterPairs.size() - 1);
    }

    private int indexOfFilter(double precursorMz, double window, int left, int right)
    {
        // Binary search for the right precursorMz
        if (left > right)
            return -1;
        int mid = (left + right) / 2;
        int compare = CompareMz(precursorMz, filterPairs.get(mid).getPrecursorMz(), window);
        if (compare < 0)
            return indexOfFilter(precursorMz, window, left, mid - 1);
        if (compare > 0)
            return indexOfFilter(precursorMz, window, mid + 1, right);

        // Scan backward until the first matching element is found.
        while (mid > 0 && CompareMz(precursorMz, filterPairs.get(mid - 1).getPrecursorMz(), window) == 0)
            mid--;

        return mid;
    }

    private static int CompareMz(double mz1, double mz2, double window)
    {
        double startMz = mz1 - window / 2;
        if (startMz < mz2 && mz2 < startMz + window)
            return 0;
        return (mz1 > mz2 ? 1 : -1);
    }



    private IsolationWindowFilter CalcDiaIsolationValues(IsolationWindowFilter isolationWindowFilter) {
        double isolationWidthValue;
        ChromatogramRequestDocument.IsolationScheme isolationScheme = chromatogramRequestDocument.getIsolationScheme();
        if (null == isolationScheme) {
            throw new IllegalStateException("Unexpected attempt to calculate DIA isolation window without an isolation scheme");
        }
        if (null != isolationScheme.getPrecursorFilter()) {
            isolationWidthValue = isolationScheme.getPrecursorFilter();
            if (null != isolationScheme.getPrecursorRightFilter()) {
                isolationWidthValue += isolationScheme.getPrecursorFilter();
            }
            // Use the user specified isolation width, unless it is larger than
            // the acquisition isolation width.  In this case the chromatograms
            // may be very confusing (spikey), because of incorrectly included
            // data points.
            if (null != isolationWindowFilter.isolationWidth && isolationWindowFilter.isolationWidth < isolationWidthValue) {
                isolationWidthValue = isolationWindowFilter.isolationWidth;
            }
            // Make sure the isolation target is centered in the desired window, even
            // if the window was specified as being asymetric
            if (null != isolationScheme.getPrecursorRightFilter()) {
                isolationWindowFilter = new IsolationWindowFilter(isolationWindowFilter.isolationMz
                        + isolationScheme.getPrecursorRightFilter() - isolationWidthValue / 2,
                        isolationWindowFilter.isolationWidth);
            }
        } else if (isolationScheme.getIsolationWindow().size() > 0) {
            // Find isolation window.
            ChromatogramRequestDocument.IsolationScheme.IsolationWindow isolationWindow = null;

            // Match pre-specified targets.
            if (null != isolationScheme.getIsolationWindow().get(0).getTarget()) {
                for (ChromatogramRequestDocument.IsolationScheme.IsolationWindow window : isolationScheme.getIsolationWindow()) {
                    if (!targetMatches(window, isolationWindowFilter.isolationMz, chromatogramRequestDocument.getMzMatchTolerance())) {
                        continue;
                    }
                    if (isolationWindow != null) {
                        throw new IllegalStateException("Two isolation windows contain targets which match the isolation target " + isolationWindow.getTarget());
                    }
                    isolationWindow = window;
                }
            } else {
                // Find containing window.
                for (ChromatogramRequestDocument.IsolationScheme.IsolationWindow window : isolationScheme.getIsolationWindow()) {
                    if (!contains(window, isolationWindowFilter.isolationMz)) {
                        continue;
                    }
                    if (null != isolationWindow) {
                        throw new IllegalStateException("Two isolation windows contain the isolation target " + isolationWindowFilter.isolationMz);
                    }
                    isolationWindow = window;
                }
            }
            if (null == isolationWindow) {
                return new IsolationWindowFilter(null, null);
            }
            isolationWidthValue = isolationWindow.getEnd() - isolationWindow.getStart();
            isolationWindowFilter = new IsolationWindowFilter(isolationWindow.getStart() + isolationWidthValue / 2, isolationWindowFilter.isolationWidth);
        } else if (null != isolationWindowFilter.isolationWidth) {
            // MSe just uses the instrument isolation window
            isolationWidthValue = isolationWindowFilter.isolationWidth;
        } else {
            throw new IllegalStateException("Isolation scheme does not contain any isolation windows");
        }
        return new IsolationWindowFilter(isolationWindowFilter.isolationMz, isolationWidthValue);
    }

    private class IsolationWindowFilter {
        public final Double isolationMz;
        public final Double isolationWidth;

        public IsolationWindowFilter(Double isolationMz, Double isolationWidth) {
            this.isolationMz = isolationMz;
            this.isolationWidth = isolationWidth;
        }

        @Override
        public boolean equals(Object o) {
            if (this == o) return true;
            if (o == null || getClass() != o.getClass()) return false;

            IsolationWindowFilter that = (IsolationWindowFilter) o;

            if (isolationMz != null ? !isolationMz.equals(that.isolationMz) : that.isolationMz != null) return false;
            if (isolationWidth != null ? !isolationWidth.equals(that.isolationWidth) : that.isolationWidth != null)
                return false;

            return true;
        }

        @Override
        public int hashCode() {
            int result = isolationMz != null ? isolationMz.hashCode() : 0;
            result = 31 * result + (isolationWidth != null ? isolationWidth.hashCode() : 0);
            return result;
        }
    }
    private boolean targetMatches(ChromatogramRequestDocument.IsolationScheme.IsolationWindow isolationWindow, double isolationTarget, double mzMatchTolerance) {
        if (null == isolationWindow.getTarget()) {
            throw new IllegalStateException("Isolation window requires a target value");
        }
        return Math.abs(isolationTarget - isolationWindow.getTarget()) <= mzMatchTolerance && contains(isolationWindow, isolationTarget);
    }
    private boolean contains(ChromatogramRequestDocument.IsolationScheme.IsolationWindow isolationWindow, double isolationTarget) {
        return isolationWindow.getStart() <= isolationTarget && isolationTarget< isolationWindow.getEnd();
    }

    public void generateChromatograms(Iterable<MsSpectrum> spectra, OutputStream outputStream) throws Exception {
        for (MsSpectrum spectrum : spectra) {
            double[] mzs = spectrum.getMzs();
            if (mzs.length == 0) {
                continue;
            }
            if (null == spectrum.getRetentionTime()) {
                continue;
            }
            double retentionTime = spectrum.getRetentionTime();
            if (!containsTime(retentionTime)) {
                continue;
            }

            if (spectrum.getMsLevel() == 1) {
                // TODO(nicksh): copy logic from SpectrumFilter.FindMs1FilterPairs.
                for (ChromatogramGroupProcessor groupProcessor : this.filterPairs) {
                    groupProcessor.processSpectrum(spectrum);
                }
            }
            if (spectrum.getMsLevel() == 2) {
                // TODO(nicksh) demultiplxer
                // Process all SRM spectra that can be generated by filtering this full-scan MS/MS
                for (IsolationWindowFilter isoWin : getIsolationWindows(spectrum.getPrecursors())) {
                    for (ChromatogramGroupProcessor filterPair : findGroupsForMs2IsolationWindow(isoWin, chromatogramRequestDocument.getMs2FullScanAcquisitionMethod(), false)) {
                        filterPair.processSpectrum(spectrum);
                    }
                }
            }
        }
        List<ChromatogramGroupPoints> pointsList = new ArrayList<ChromatogramGroupPoints>();
        for (ChromatogramGroupProcessor group : this.filterPairs) {
            pointsList.add(group.getChromatogramGroupPoints());
        }
        SkydWriter.writeSkydFile(pointsList, outputStream);
    }

    public static void processRequest(MsDataFile msData, InputStream inputStream, OutputStream outputStream) throws Exception {
        ChromatogramRequestDocument spectrumFilterDocument =
                (ChromatogramRequestDocument) JAXBContext.newInstance(ChromatogramRequestDocument.class)
                        .createUnmarshaller()
                        .unmarshal(inputStream);
        generateChromatograms(msData, spectrumFilterDocument, outputStream);
    }
    public static void generateChromatograms(MsDataFile msDataFile, ChromatogramRequestDocument spectrumFilterDocument,
                                             OutputStream outputStream) throws Exception {
        new ChromatogramGenerator(spectrumFilterDocument).generateChromatograms(msDataFile.getSpectra(), outputStream);
    }

}
