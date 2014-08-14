package edu.uw.gs.skyline.cloudapi.chromatogramgenerator;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromatogramRequestDocument;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

/**
 * Created by nicksh on 3/19/2014.
 */
public class ChromatogramGroupPoints implements GroupPoints {
    private final ChromatogramRequestDocument.ChromatogramGroup chromatogramGroupInfo;
    private List<Float> times;
    private List<Integer> scanIds;
    private List<float[]> intensitiesList;
    private List<float[]> relativeMassErrorsList;

    public ChromatogramGroupPoints(ChromatogramRequestDocument.ChromatogramGroup chromatogramGroupInfo) {
        this.chromatogramGroupInfo = chromatogramGroupInfo;
        times = new ArrayList<Float>();
        scanIds = new ArrayList<Integer>();
        intensitiesList = new ArrayList<float[]>();
        if (chromatogramGroupInfo.isMassErrors()) {
            relativeMassErrorsList = new ArrayList<float[]>();
        }
    }

    @Override
    public ChromatogramRequestDocument.ChromatogramGroup getChromatogramGroupInfo() {
        return chromatogramGroupInfo;
    }

    @Override
    public void addPoint(double retentionTime, Integer scanId, List<ChromatogramPointValue> values) {
        int expectedArrayLength = chromatogramGroupInfo.getChromatogram().size();
        if (expectedArrayLength != values.size()) {
            throw new IllegalArgumentException("Values size is " + values.size() + " should be " + expectedArrayLength);
        }
        if (null == scanId) {
            if (0 != scanIds.size()) {
                throw new IllegalArgumentException("scanId cannot be null because scan ids have already been added");
            }
        } else {
            if (0 == scanIds.size() && 0 != times.size()) {
                throw new IllegalArgumentException("scanId cannot have value because earlier scanIds were null");
            }
            scanIds.add(scanId);
        }
        times.add((float) retentionTime);
        float[] intensities = new float[values.size()];
        for (int i = 0; i < values.size(); i++) {
            intensities[i] = (float) values.get(i).getIntensity();
        }
        intensitiesList.add(intensities);
        if (relativeMassErrorsList != null) {
            float[] relativeMassErrors = new float[values.size()];
            for (int i = 0; i < values.size(); i++) {
                relativeMassErrors[i] = (float) values.get(i).getRelativeMassError();
            }
            relativeMassErrorsList.add(relativeMassErrors);
        }
    }

    @Override
    public byte[] toByteArray() {
        if (times.size() == 0) {
            return new byte[0];
        }
        try {
            int transitionCount = intensitiesList.get(0).length;
            ByteArrayOutputStream byteArrayOutputStream = new ByteArrayOutputStream();
            StreamWrapper streamWrapper = new StreamWrapper(byteArrayOutputStream);
            for (float time : times) {
                streamWrapper.writeFloat(time);
            }
            for (int i = 0; i < transitionCount; i++) {
                for (float[] intensities : intensitiesList) {
                    streamWrapper.writeFloat(intensities[i]);
                }
            }
            if (null != relativeMassErrorsList) {
                for (int i = 0; i < transitionCount; i++) {
                    for (float[] massErrors : relativeMassErrorsList) {
                        // Write out the mass error as a short integer equal to the relative mass error times 10 million.
                        streamWrapper.writeShort((short) (massErrors[i] * 10 * 1000 * 1000 + 0.5));
                    }
                }
            }
            if (hasScanIds()) {
                for (int scanId : scanIds) {
                    streamWrapper.writeInt(scanId);
                }
            }
            return byteArrayOutputStream.toByteArray();
        } catch (IOException ioException) {
            throw new RuntimeException(ioException);
        }
    }

    @Override
    public int getPointCount() {
        return times.size();
    }

    @Override
    public boolean hasMassErrors() {
        return null != relativeMassErrorsList;
    }

    public boolean hasScanIds() {
        return scanIds.size() > 0;
    }
}
