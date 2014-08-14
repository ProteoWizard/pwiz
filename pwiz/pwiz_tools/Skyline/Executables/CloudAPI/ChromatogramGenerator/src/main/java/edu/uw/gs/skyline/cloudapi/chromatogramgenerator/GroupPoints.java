package edu.uw.gs.skyline.cloudapi.chromatogramgenerator;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromatogramRequestDocument;

import java.util.List;

/**
 * @author Oleksii Tymchenko
 */
public interface GroupPoints {
    ChromatogramRequestDocument.ChromatogramGroup getChromatogramGroupInfo();

    void addPoint(double retentionTime, Integer scanId, List<ChromatogramPointValue> values);

    byte[] toByteArray();

    int getPointCount();

    boolean hasMassErrors();

    boolean hasScanIds();
}
