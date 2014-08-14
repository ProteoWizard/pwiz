package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data;

import java.util.Collections;
import java.util.Date;

/**
 * Created by nicksh on 3/4/14.
 */
public class MsDataFile {
    private Iterable<MsSpectrum> spectra = Collections.emptyList();
    public Iterable<MsSpectrum> getSpectra() {
        return spectra;
    }
    public void setSpectra(Iterable<MsSpectrum> spectra) {
        this.spectra = spectra;
    }
}
