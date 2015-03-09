package com.infoclinika.chorus.integration.skyline.api;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.GroupPoints;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.SkydWriter;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromatogramRequestDocument;
import org.apache.log4j.Logger;

import javax.xml.bind.JAXBContext;
import javax.xml.bind.JAXBException;
import javax.xml.bind.Unmarshaller;
import java.io.OutputStream;
import java.io.StringReader;
import java.util.Arrays;
import java.util.Set;

/**
 * @author Oleksii Tymchenko
 */
public abstract class ChromatogramExtractor {
    private final Logger LOGGER = Logger.getLogger(ChromatogramExtractor.class);

    /**
     * Extract chromatograms according to the incoming request for the specified raw file reference.
     * <p/>
     * To be implemented in the Chorus codebase.
     *
     * @param ms1FilterRef  the reference to the translated MS filter contents
     * @param ms2FilterRefs the references to the translated MS/MS filter contents
     * @param request       incoming extraction request
     * @return collection of the chromatogram group points
     */
    protected abstract Iterable<GroupPoints> extract(String ms1FilterRef, Set<String> ms2FilterRefs, ChromatogramRequestDocument request);

    /**
     * Public endpoint for the chromatogram extraction callee.
     * <p/>
     * Serves for chromatogram extraction; feeds the resulting content to the specified output stream.
     *
     * @param xmlRequest    extraction request contents as plain string (for simplicity)
     * @param ms1FilterRef  an associated reference of the MS translated filter contents
     * @param ms2FilterRefs an associated references of the MS/MS translated filter contents
     * @param destination   the output stream serving as destination for the .chorusresponse resulting content
     * @throws Exception in case of unexpected errors while writing the results
     */
    public void processExtractionRequest(final String xmlRequest, final String ms1FilterRef, final Set<String> ms2FilterRefs,
                                         final OutputStream destination) throws Exception {
        LOGGER.debug("Extracting chromatograms for functions. MS1: " + ms1FilterRef + ". MS2: " + Arrays.toString(ms2FilterRefs.toArray()));
        final ChromatogramRequestDocument requestDocument = parseRequest(xmlRequest);
        final Iterable<GroupPoints> results = extract(ms1FilterRef, ms2FilterRefs, requestDocument);

        SkydWriter.writeSkydFile(results, destination);
    }

    private ChromatogramRequestDocument parseRequest(String xmlRequest) {
        final ChromatogramRequestDocument requestDocument;
        try {
            final Unmarshaller spectrumFilterUnmarshaller = JAXBContext.newInstance(ChromatogramRequestDocument.class).createUnmarshaller();
            requestDocument = (ChromatogramRequestDocument) spectrumFilterUnmarshaller.unmarshal(new StringReader(xmlRequest));
            LOGGER.debug("Parsing of the request finished. Total chromatogram groups: " + requestDocument.getChromatogramGroup().size());
        } catch (JAXBException e) {
            throw new RuntimeException("Failed to parse incoming request", e);
        }
        return requestDocument;
    }
}
