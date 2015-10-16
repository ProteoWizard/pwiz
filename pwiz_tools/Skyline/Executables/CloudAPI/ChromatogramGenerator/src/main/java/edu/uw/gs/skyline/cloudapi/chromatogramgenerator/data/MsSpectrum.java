package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data;

/**
 * Created by nicksh on 3/4/14.
 */
public class MsSpectrum {
    private double[] mzs;
    private double[] intensities;
    boolean centroided;
    Double retentionTime;
    Double driftTime;
    Integer scanId;
    int msLevel;
    MsPrecursor[] precursors = new MsPrecursor[0];

    public double[] getMzs() {
        return mzs;
    }

    public void setMzs(double[] mzs) {
        this.mzs = mzs;
    }

    public double[] getIntensities() {
        return intensities;
    }

    public void setIntensities(double[] intensities) {
        this.intensities = intensities;
    }

    public boolean isCentroided() {
        return centroided;
    }

    public void setCentroided(boolean centroided) {
        this.centroided = centroided;
    }

    public Double getRetentionTime() {
        return retentionTime;
    }

    public void setRetentionTime(Double retentionTime) {
        this.retentionTime = retentionTime;
    }

    public Integer getScanId() {
        return scanId;
    }

    public void setScanId(Integer scanId) {
        this.scanId = scanId;
    }

    public int getMsLevel() {
        return msLevel;
    }

    public void setMsLevel(int msLevel) {
        this.msLevel = msLevel;
    }

    public Double getDriftTime() {
        return driftTime;
    }

    public void setDriftTime(Double driftTime) {
        this.driftTime = driftTime;
    }

    public MsPrecursor[] getPrecursors() {
        return precursors;
    }
    public void setPrecursors(MsPrecursor[] precursors) {
        this.precursors = precursors;
    }
}
