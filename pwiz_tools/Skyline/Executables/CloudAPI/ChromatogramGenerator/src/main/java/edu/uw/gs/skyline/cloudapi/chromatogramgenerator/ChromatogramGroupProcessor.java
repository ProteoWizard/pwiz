package edu.uw.gs.skyline.cloudapi.chromatogramgenerator;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.*;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromSource;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsSpectrum;

import java.util.*;

/**
 * Created by nicksh on 3/5/14.
 */
class ChromatogramGroupProcessor {
    private final ChromatogramRequestDocument.ChromatogramGroup chromatogramGroup;
    private final ChromatogramGroupPoints chromatogramGroupPoints;


    public ChromatogramGroupProcessor(ChromatogramRequestDocument.ChromatogramGroup chromatogramGroup) {
        this.chromatogramGroup = chromatogramGroup;
        this.chromatogramGroupPoints = new ChromatogramGroupPoints(chromatogramGroup);
    }

    public boolean containsTime(double time) {
        if (chromatogramGroup.getMinTime() != null && chromatogramGroup.getMinTime() > time) {
            return false;
        }
        if (chromatogramGroup.getMaxTime() != null && chromatogramGroup.getMaxTime() < time) {
            return false;
        }
        return true;
    }

    public ChromatogramGroupPoints getChromatogramGroupPoints() {
        return chromatogramGroupPoints;
    }

    public ChromSource getChromSource() {
        return chromatogramGroup.getSource();
    }

    public void processSpectrum(MsSpectrum msSpectrum) {
        Double retentionTime = msSpectrum.getRetentionTime();
        if (null == retentionTime || !containsTime(retentionTime)) {
            return;
        }
        if (null != msSpectrum.getDriftTime() && null != chromatogramGroup.getDriftTime() && null != chromatogramGroup.getDriftTimeWindow()) {
            double driftTime = msSpectrum.getDriftTime();
            double minDriftTime = chromatogramGroup.getDriftTime() - chromatogramGroup.getDriftTimeWindow() / 2;
            double maxDriftTime = chromatogramGroup.getDriftTime() + chromatogramGroup.getDriftTimeWindow() / 2;
            if (driftTime < minDriftTime || driftTime > maxDriftTime) {
                return;
            }
        }
        switch (msSpectrum.getMsLevel()) {
            case 1:
                if (getChromSource() != ChromSource.MS_1) {
                    return;
                }
                break;
            case 2:
                if (getChromSource() != ChromSource.MS_2) {
                    return;
                }
                break;
            default:
                return;
        }
        processSpectrumAndAddPoint(retentionTime, msSpectrum.getScanId(), msSpectrum.getMzs(), msSpectrum.getIntensities(), chromatogramGroupPoints);
    }

    public boolean processSpectrumAndAddPoint(double retentionTime,
                                              Integer scanId,
                                              double[] mzArray,
                                              double[] intensityArray,
                                              ChromatogramGroupPoints chromatogramGroupPoints) {
        List<ChromatogramPointValue> values = new ArrayList<ChromatogramPointValue>();
        for (ChromatogramRequestDocument.ChromatogramGroup.Chromatogram chromatogram : chromatogramGroup.getChromatogram()) {
            values.add(ChromatogramPointValue.calculate(mzArray, intensityArray, chromatogram.getProductMz(), chromatogramGroup.getExtractor(), chromatogram.getMzWindow()));
        }
        chromatogramGroupPoints.addPoint((float) retentionTime, scanId, values);
        return true;
    }

    public double getPrecursorMz() {
        return chromatogramGroup.getPrecursorMz();
    }
}
