package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data;

/**
 * Created by nicksh on 3/4/14.
 */
public class MsPrecursor {
    Double precursorMz;
    Double precursorCollisionEnergy;
    Double isolationWindowTargetMz;
    Double isolationWindowUpper;
    Double isolationWindowLower;

    public Double getPrecursorMz() {
        return precursorMz;
    }

    public void setPrecursorMz(Double precursorMz) {
        this.precursorMz = precursorMz;
    }

    public Double getPrecursorCollisionEnergy() {
        return precursorCollisionEnergy;
    }

    public void setPrecursorCollisionEnergy(Double precursorCollisionEnergy) {
        this.precursorCollisionEnergy = precursorCollisionEnergy;
    }

    public Double getIsolationWindowTargetMz() {
        return isolationWindowTargetMz;
    }

    public void setIsolationWindowTargetMz(Double isolationWindowTargetMz) {
        this.isolationWindowTargetMz = isolationWindowTargetMz;
    }

    public Double getIsolationWindowUpper() {
        return isolationWindowUpper;
    }

    public void setIsolationWindowUpper(Double isolationWindowUpper) {
        this.isolationWindowUpper = isolationWindowUpper;
    }

    public Double getIsolationWindowLower() {
        return isolationWindowLower;
    }

    public void setIsolationWindowLower(Double isolationWindowLower) {
        this.isolationWindowLower = isolationWindowLower;
    }

    public Double getIsolationWidth() {
        if (null == isolationWindowLower || null == isolationWindowUpper) {
            return null;
        }
        double width = isolationWindowUpper + isolationWindowLower;
        if (width > 0) {
            return width;
        }
        return null;
    }
}
