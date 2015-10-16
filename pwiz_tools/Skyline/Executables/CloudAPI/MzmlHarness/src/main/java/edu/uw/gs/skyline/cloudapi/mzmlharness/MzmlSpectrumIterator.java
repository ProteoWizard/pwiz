package edu.uw.gs.skyline.cloudapi.mzmlharness;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromatogramRequestDocument;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.Ms2FullScanAcquisitionMethod;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsPrecursor;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsSpectrum;
import uk.ac.ebi.jmzml.model.mzml.BinaryDataArray;
import uk.ac.ebi.jmzml.model.mzml.CVParam;
import uk.ac.ebi.jmzml.model.mzml.FileDescription;
import uk.ac.ebi.jmzml.model.mzml.ParamGroup;
import uk.ac.ebi.jmzml.model.mzml.Precursor;
import uk.ac.ebi.jmzml.model.mzml.Scan;
import uk.ac.ebi.jmzml.model.mzml.SourceFile;
import uk.ac.ebi.jmzml.model.mzml.Spectrum;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller;

import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;

/**
 * Created by nicksh on 3/12/14.
 */
public class MzmlSpectrumIterator implements Iterator<MsSpectrum> {
    private final MzMLUnmarshaller unmarshaller;
    private final ChromatogramRequestDocument spectrumFilterDocument;
    private final boolean isWatersMse;
    private Iterator<Spectrum> iterator;
    private int mseLevel;
    private MsSpectrum mseLastSpectrum;

    public MzmlSpectrumIterator(MzMLUnmarshaller unmarshaller, ChromatogramRequestDocument spectrumFilterDocument) {
        this.unmarshaller = unmarshaller;
        this.spectrumFilterDocument = spectrumFilterDocument;
        boolean isWatersMse = false;
        if (spectrumFilterDocument.getMs2FullScanAcquisitionMethod() == Ms2FullScanAcquisitionMethod.DIA) {
            String specialHandling = spectrumFilterDocument.getIsolationScheme().getSpecialHandling();
            if ("MSe".equals(specialHandling) || "All Ions".equals(specialHandling)) {
                FileDescription fd = unmarshaller.unmarshalFromXpath("/fileDescription", FileDescription.class);
                for (SourceFile sourceFile : fd.getSourceFileList().getSourceFile()) {
                    if (null != findCvParam(sourceFile.getCvParam(), "Waters raw file")) {
                        isWatersMse = true;
                        this.mseLevel = 1;
                        break;
                    }
                }
            }
        }

        this.isWatersMse = isWatersMse;
        this.iterator =  unmarshaller.unmarshalCollectionFromXpath("/run/spectrumList/spectrum", Spectrum.class);
    }

    @Override
    public boolean hasNext() {
        return iterator.hasNext();
    }

    @Override
    public MsSpectrum next() {
        Spectrum spectrum = iterator.next();
        MsSpectrum msSpectrum = new MsSpectrum();
        msSpectrum.setScanId(spectrum.getIndex());
        for (BinaryDataArray binaryDataArray : spectrum.getBinaryDataArrayList().getBinaryDataArray()) {
            switch (binaryDataArray.getDataType()) {
                case MZ_VALUES:
                    msSpectrum.setMzs(toDoubleArray(binaryDataArray.getBinaryDataAsNumberArray()));
                    break;
                case INTENSITY:
                    msSpectrum.setIntensities(toDoubleArray(binaryDataArray.getBinaryDataAsNumberArray()));
                    break;
            }
        }
        msSpectrum.setCentroided(null != findCvParam(spectrum.getCvParam(), "centroid spectrum"));
        for (Scan scan : spectrum.getScanList().getScan()) {
            Double retentionTime = getMinuteValue(scan.getCvParam(), "scan start time");
            if (null != retentionTime) {
                msSpectrum.setRetentionTime(retentionTime);
                break;
            }
        }
        for (Scan scan : spectrum.getScanList().getScan()) {
            Double driftTime = getDoubleValue(scan.getCvParam(), "ion mobility drift time");
            if (null != driftTime) {
                msSpectrum.setDriftTime(driftTime);
                break;
            }
        }
        if (0 == mseLevel) {
            Integer msLevel = getIntegerValue(spectrum.getCvParam(), "ms level");
            if (null != msLevel) {
                msSpectrum.setMsLevel(msLevel);
            }
            List<MsPrecursor> precursors = new ArrayList<MsPrecursor>();
            if (null != spectrum.getPrecursorList()) {
                for (Precursor precursor : spectrum.getPrecursorList().getPrecursor()) {
                    MsPrecursor msPrecursor = new MsPrecursor();
                    for (ParamGroup selectedIon : precursor.getSelectedIonList().getSelectedIon()) {
                        msPrecursor.setPrecursorMz(getDoubleValue(selectedIon.getCvParam(), "selected ion m/z"));
                        break;
                    }
                    if (null != precursor.getActivation()) {
                        msPrecursor.setPrecursorCollisionEnergy(getDoubleValue(precursor.getActivation().getCvParam(), "collision energy"));
                    }
                    msPrecursor.setIsolationWindowTargetMz(getDoubleValue(precursor.getIsolationWindow().getCvParam(), "isolation window target m/z"));
                    msPrecursor.setIsolationWindowLower(getDoubleValue(precursor.getIsolationWindow().getCvParam(), "isolation window lower offset"));
                    msPrecursor.setIsolationWindowUpper(getDoubleValue(precursor.getIsolationWindow().getCvParam(), "isolation_window_upper_offset"));
                    precursors.add(msPrecursor);
                }
            }
            msSpectrum.setPrecursors(precursors.toArray(new MsPrecursor[precursors.size()]));
        } else {
            if (mseLastSpectrum != null) {
                double lastRetentionTime = null != mseLastSpectrum.getRetentionTime() ? mseLastSpectrum.getRetentionTime() : 0;
                double retentionTime = null != msSpectrum.getRetentionTime() ? msSpectrum.getRetentionTime() : 0;
                if (retentionTime < lastRetentionTime) {
                    mseLevel++;
                }
            }
            mseLastSpectrum = msSpectrum;
            msSpectrum.setMsLevel(mseLevel);
            // Waters MSe high-energy scans actually appear to be MS1 scans without
            // any isolation m/z.  So, use the instrument range.
            MsPrecursor msPrecursor = new MsPrecursor();
            msPrecursor.setIsolationWindowTargetMz((spectrumFilterDocument.getMinMz() + spectrumFilterDocument.getMaxMz()) / 2.0);
            msPrecursor.setIsolationWindowLower((double) spectrumFilterDocument.getMinMz());
            msPrecursor.setIsolationWindowUpper((double) spectrumFilterDocument.getMaxMz());
            msSpectrum.setPrecursors(new MsPrecursor[] {msPrecursor});
        }
        return msSpectrum;
    }

    @Override
    public void remove() {
        iterator.remove();
    }

    private static double[] toDoubleArray(Number[] numbers) {
        double[] result = new double[numbers.length];
        for (int i = 0; i < result.length; i++) {
            result[i] = numbers[i].doubleValue();
        }
        return result;
    }

    private static String getStringValue(List<CVParam> cvParams, String name) {
        CVParam cvParam = findCvParam(cvParams, name);
        if (null == cvParam) {
            return null;
        }
        return cvParam.getValue();
    }
    private static Double getDoubleValue(List<CVParam> cvParams, String name) {
        String str = getStringValue(cvParams, name);
        if (null == str) {
            return null;
        }
        return Double.parseDouble(str);
    }
    private static Double getMinuteValue(List<CVParam> cvParams, String name) {
        CVParam cvParam = findCvParam(cvParams, name);
        if (null == cvParam) {
            return null;
        }
        double value = Double.parseDouble(cvParam.getValue());
        if ("second".equals(cvParam.getUnitName())) {
            value /= 60;
        }
        return value;
    }
    private static Integer getIntegerValue(List<CVParam> cvParams, String name) {
        String str = getStringValue(cvParams, name);
        if (null == str) {
            return null;
        }
        return Integer.parseInt(str);
    }
    private static CVParam findCvParam(List<CVParam> cvParams, String name) {
        for (CVParam cvParam : cvParams) {
            if (cvParam.getName().equals(name)) {
                return cvParam;
            }
        }
        return null;
    }

}
