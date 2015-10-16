package edu.uw.gs.skyline.cloudapi.chromatogramgenerator;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromExtractor;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromSource;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromatogramRequestDocument;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.zip.Deflater;

/**
 * Writes out a Skyline .skyd file.
 * <p>A .skyd file stores chromatogram data (time, intensity, and optional mass error).</p>
 * <p>In Skyline, the .skyd file also stores all of the peaks that have been detected in the chromatograms, and "scores"
 * that have been calculated from looking at the shape of chromatogram peaks, but in this Java implementation, the number
 * of peaks, and scores is always zero.</p>
 * <p>A Chromatogram Group is a set of chromatograms that are all for the same peptide sequence, precursor m/z.
 * Also, in this Java implementation, all chromatograms in a Chromatogram Group have the same {@link ChromSource}.</p>
 * <p>All of the point values for a chromatogram group are compressed together.  The chromatogram group data is written
 * out at the start of the .skyd file, and the location in the file where each group's points are written is remembered
 * in {@link ChromGroupHeader#locationPoints}.</p>
 */
public class SkydWriter {
    /** All skyd files write their version number exactly 52 bytes before the end of the file. */
    public static final int FORMAT_VERSION_CACHE = 8;
    private final StreamWrapper stream;
    private List<ChromTransition> transitions = new ArrayList<ChromTransition>();
    private List<ChromGroupHeader> chromGroupHeaders = new ArrayList<ChromGroupHeader>();
    ByteArrayOutputStream sequenceBytes = new ByteArrayOutputStream();
    Map<String, Integer> sequenceIndexes = new HashMap<String,Integer>();
    public SkydWriter(StreamWrapper stream) {
        this.stream = stream;
    }
    public void writeChromData(GroupPoints chromatogramPoints) throws Exception {
        ChromatogramRequestDocument.ChromatogramGroup chromatogramGroupInfo = chromatogramPoints.getChromatogramGroupInfo();
        ChromGroupHeader chromGroupHeader = new ChromGroupHeader();
        chromGroupHeader.calcSeqIndex(chromatogramGroupInfo.getModifiedSequence());
        chromGroupHeader.startTransitionIndex = transitions.size();
        chromGroupHeader.locationPoints = stream.getPosition();
        chromGroupHeader.numTransitions = chromatogramGroupInfo.getChromatogram().size();
        chromGroupHeader.numPoints = chromatogramPoints.getPointCount();
        chromGroupHeader.hasMassErrors = chromatogramPoints.hasMassErrors();
        chromGroupHeader.extracted_base_peak = chromatogramGroupInfo.getExtractor() == ChromExtractor.BASE_PEAK;
        chromGroupHeader.precursor = chromatogramGroupInfo.getPrecursorMz();
        chromGroupHeader.hasCalculatedMzs = true;

        if (chromatogramPoints.hasScanIds()) {
            switch (chromatogramPoints.getChromatogramGroupInfo().getSource()) {
                case MS_1:
                    chromGroupHeader.has_ms1_scan_ids = true;
                    break;
                case SIM:
                    chromGroupHeader.has_sim_scan_ids = true;
                    break;
                case MS_2:
                    chromGroupHeader.has_frag_scan_ids = true;
                    break;
            }
        }

        for (ChromatogramRequestDocument.ChromatogramGroup.Chromatogram transition : chromatogramGroupInfo.getChromatogram()) {
            ChromTransition chromTransition = new ChromTransition();
            chromTransition.chromSource = chromatogramGroupInfo.getSource();
            chromTransition.product = transition.getProductMz();
            chromTransition.extractionWidth = (float) transition.getMzWindow();
            if (null != chromatogramGroupInfo.getDriftTime()) {
                chromTransition.ionMobilityValue = chromatogramGroupInfo.getDriftTime().floatValue();
            }
            if (null != chromatogramGroupInfo.getDriftTimeWindow()) {
                chromTransition.ionMobilityExtractionWidth = chromatogramGroupInfo.getDriftTimeWindow().floatValue();
            }
            transitions.add(chromTransition);
        }
        byte[] points = chromatogramPoints.toByteArray();
        byte[] pointsCompressed = compress(points);
        if (pointsCompressed.length >= points.length) {
            pointsCompressed = points;
        }
        chromGroupHeader.compressedSize = pointsCompressed.length;
        stream.write(pointsCompressed);
        chromGroupHeaders.add(chromGroupHeader);
    }

    /**
     * Write out the header information at the end of the file.
     * The bulk of the data in a .skyd file is written in {@link #writeChromData(GroupPoints)}.
     * Then, the list of {@link ChromTransition}, {@link ChromGroupHeader}, and the block of bytes that contain
     * all of the strings shared by the ChromGroupHeaders.  Skyd files from this class only ever contain data from
     * one MS data file, but Skyline Skyd files will also have a list of "ChromCachedFile" structures with the file
     * names, modified time, etc.
     *
     * The very end of file contains the Skyd file headers, which is located a fixed offset from the end of the file
     * and has fields which are absolute positions (long integers) for where to find the ChromGroupHeaders, etc.
     * @throws Exception
     */
    public void finish() throws Exception {

        long transitionLocation = stream.getPosition();
        for (ChromTransition transition : transitions) {
            transition.write(stream);
        }
        long chromGroupHeadersLocation = stream.getPosition();
        for (ChromGroupHeader chromGroupHeader : chromGroupHeaders) {
            chromGroupHeader.write(stream);
        }
        long sequenceLocation = stream.getPosition();
        stream.write(sequenceBytes.toByteArray());
        // Skyd files produced by this class never contain peaks or scores.
        final int peakCount = 0;
        final long locationPeaks = 0;
        final int scoreTypeCount = 0;
        final int scoreCount = 0;
        final long scoresLocation = 0;

        long filesLocation = stream.getPosition();
        int filesCount = 1;
        // The one file in this .skyd
        long modifiedTime = 0;
        long runStartTime = 0;
        // If we cared about the file name, or the instrument info, they would be encoded in UTF-8
        byte[] fileNameBytes = new byte[0];
        byte[] instrumentInfoBytes = new byte[0];
        stream.writeLong(modifiedTime);
        stream.writeInt(fileNameBytes.length);
        stream.writeLong(runStartTime);
        stream.writeInt(instrumentInfoBytes.length);
        stream.writeInt(0); // flags
        stream.writeInt(0); // max_retention_time
        stream.writeInt(0); // max_intensity
        stream.write(fileNameBytes);
        // instrument info
        stream.write(instrumentInfoBytes);

        // Write out the .skyd file header.  It contains information about where to find the list of ChromGroupHeaders,
        // etc.
        stream.writeInt(scoreTypeCount);
        stream.writeInt(scoreCount);
        stream.writeLong(scoresLocation);
        stream.writeInt(sequenceBytes.size());
        stream.writeLong(sequenceLocation);
        stream.writeInt(FORMAT_VERSION_CACHE);
        stream.writeInt(peakCount);
        stream.writeLong(locationPeaks);
        stream.writeInt(transitions.size());
        stream.writeLong(transitionLocation);
        stream.writeInt(chromGroupHeaders.size());
        stream.writeLong(chromGroupHeadersLocation);
        stream.writeInt(filesCount);
        stream.writeLong(filesLocation);
    }

    public static void writeSkydFile(Iterable<? extends GroupPoints> chromatogramGroupPointsCollection, OutputStream outputStream) throws Exception {
        SkydWriter skydWriter = new SkydWriter(new StreamWrapper(outputStream));
        for (GroupPoints chromatogramGroupPoints : chromatogramGroupPointsCollection) {
            skydWriter.writeChromData(chromatogramGroupPoints);
        }
        skydWriter.finish();
    }

    /**
     * Holds information about a group of chromatograms in the .skyd file.  All of the chromatogram data for a
     * chromatogram group is stored in one block that has one set of retention times and is compressed together.
     * The location in the file of that data is at {@link #locationPoints}.
     */
    private class ChromGroupHeader {
        public int seqIndex;
        public int startTransitionIndex;
        public int startPeakIndex;
        public int startScoreIndex;
        public int numPoints;
        public int compressedSize;
        public boolean hasMassErrors;
        public boolean hasCalculatedMzs;
        public boolean extracted_base_peak;
        public boolean has_ms1_scan_ids;
        public boolean has_sim_scan_ids;
        public boolean has_frag_scan_ids;
        public int fileIndex;
        public int seqLen;
        public int numTransitions;
        public int numPeaks;
        public int maxPeakIndex;
        public int statusId;
        public int statusRank;
        public double precursor;
        public long locationPoints;
        public void write(StreamWrapper stream) throws Exception {
            stream.writeInt(seqIndex);
            stream.writeInt(startTransitionIndex);
            stream.writeInt(startPeakIndex);
            stream.writeInt(startScoreIndex);
            stream.writeInt(numPoints);
            stream.writeInt(compressedSize);
            short flagBits = 0;
            if (hasMassErrors) {
                flagBits |= 0x01;
            }
            if (hasCalculatedMzs) {
                flagBits |= 0x02;
            }
            if (extracted_base_peak) {
                flagBits |= 0x04;
            }
            if (has_ms1_scan_ids) {
                flagBits |= 0x08;
            }
            if (has_sim_scan_ids) {
                flagBits |= 0x10;
            }
            if (has_frag_scan_ids) {
                flagBits |= 0x20;
            }
            stream.writeShort(flagBits);
            stream.writeShort((short) (fileIndex & 0xffff));
            stream.writeShort((short) (seqLen & 0xffff));
            stream.writeShort((short) (numTransitions & 0xffff));
            stream.writeByte((byte)(numPeaks & 0xff));
            stream.writeByte((byte)(maxPeakIndex & 0xff));
            stream.writeShort((short) 0);
            stream.writeShort((short) (statusId & 0xffff));
            stream.writeShort((short) (statusRank & 0xffff));
            stream.writeDouble(precursor);
            stream.writeLong(locationPoints);
        }

        /**
         * Determines if a string is present in the dictionary, or whether it needs to be added, and its bytes appeanded
         * to the ByteArrayOutputStream.
         * All of the strings in a skyd file are stored in a big array of bytes.
         * Strings are referred to by {@link #seqIndex} and {@link #seqLen}.
         */
        public void calcSeqIndex(String sequence) {
            if (sequence == null) {
                seqIndex = -1;
                seqLen = 0;
            } else {
                seqLen = sequence.length();
                Integer seqIndex = SkydWriter.this.sequenceIndexes.get(sequence);
                if (null == seqIndex) {
                    seqIndex = SkydWriter.this.sequenceBytes.size();
                    SkydWriter.this.sequenceIndexes.put(sequence, seqIndex);
                    for (char ch : sequence.toCharArray()) {
                        SkydWriter.this.sequenceBytes.write((byte) ch);
                    }
                }
                this.seqIndex = seqIndex;
            }
        }
    }

    /**
     * Holds the information about a single chromatogram in a {@link ChromGroupHeader}.
     * A ChromTransition is always 16 bytes in the .skyd file.
     * All of the ChromTransitions in a .skyd file are written out in one block, and the ChromGroupHeader's
     * startTransitionIndex and numTransitions specifies which transitions belong to the Chromatogram Group.
     */
    private class ChromTransition {
        public double product;
        public float extractionWidth;
        public float ionMobilityValue;
        public float ionMobilityExtractionWidth;
        public ChromSource chromSource;
        public void write(StreamWrapper stream) throws Exception {
            stream.writeDouble(product);
            stream.writeFloat(extractionWidth);
            stream.writeFloat(ionMobilityValue);
            stream.writeFloat(ionMobilityExtractionWidth);
            short flagBits;
            switch (chromSource) {
                default:
                    flagBits = 0;
                    break;
                case MS_2:
                    flagBits = 0x02;
                    break;
                case MS_1:
                    flagBits = 0x01;
                    break;
                case SIM:
                    flagBits = 0x03;
                    break;
            }
            stream.writeShort(flagBits);
            stream.writeShort((short) 0);
        }
    }
    private static byte[] compress(byte[] bytes) throws IOException {
        ByteArrayOutputStream byteArrayOutputStream = new ByteArrayOutputStream();
        Deflater deflater = new Deflater(Deflater.BEST_COMPRESSION);
        try {
            deflater.setInput(bytes);
            deflater.finish();
            while (!deflater.finished()) {
                byte[] buffer = new byte[bytes.length];
                int count = deflater.deflate(buffer);
                byteArrayOutputStream.write(buffer, 0, count);
            }
            return byteArrayOutputStream.toByteArray();
        } finally {
            deflater.end();
        }
    }
}
