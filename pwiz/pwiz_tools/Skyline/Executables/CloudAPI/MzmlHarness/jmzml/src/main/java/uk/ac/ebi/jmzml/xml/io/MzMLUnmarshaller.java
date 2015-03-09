/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller
 *
 * jmzml is Copyright 2008 The European Bioinformatics Institute
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

package uk.ac.ebi.jmzml.xml.io;

import org.apache.commons.collections.buffer.CircularFifoBuffer;
import org.apache.log4j.Logger;
import org.xml.sax.InputSource;
import psidev.psi.tools.xxindex.index.IndexElement;
import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.*;
import uk.ac.ebi.jmzml.model.mzml.utilities.ModelConstants;
import uk.ac.ebi.jmzml.xml.jaxb.unmarshaller.UnmarshallerFactory;
import uk.ac.ebi.jmzml.xml.jaxb.unmarshaller.filters.MzMLNamespaceFilter;
import uk.ac.ebi.jmzml.xml.util.EscapingXMLUtilities;
import uk.ac.ebi.jmzml.xml.xxindex.FileUtils;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer;
import uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexerFactory;

import javax.xml.bind.JAXBElement;
import javax.xml.bind.JAXBException;
import javax.xml.bind.Unmarshaller;
import javax.xml.transform.sax.SAXSource;
import java.io.*;
import java.net.URL;
import java.security.DigestInputStream;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class MzMLUnmarshaller {

    private static final Logger logger = Logger.getLogger(MzMLUnmarshaller.class);
    private static final char[] HEX_CHARS = "0123456789abcdef".toCharArray();

    private final File mzMLFile;
    private final MzMLIndexer index;
    private final boolean useSpectrumCache;
    //    private final AdapterObjectCache cache = new AdapterObjectCache();
    private final MzMLObjectCache cache;

    private IndexList indexList = null;
    private boolean fileCorrupted = false;

    private final Pattern ID_PATTERN = Pattern.compile("id *= *\"([^\"]*)?\"", Pattern.CASE_INSENSITIVE);
    private final Pattern AC_PATTERN = Pattern.compile("accession *= *\"([^\"]*)?\"", Pattern.CASE_INSENSITIVE);
    private final Pattern VERSION_PATTERN = Pattern.compile("version *= *\"([^\"]*)?\"", Pattern.CASE_INSENSITIVE);
    private static final Pattern XML_ATT_PATTERN = Pattern.compile("\\s+([A-Za-z:]+)\\s*=\\s*[\"']([^\"'>]+?)[\"']", Pattern.DOTALL);

    /**
     * Creates a new MzMLUnmarshaller object from a URL
     *
     * @param mzMLFileURL the URL to unmarshall
     */
    public MzMLUnmarshaller(URL mzMLFileURL) {
        this(mzMLFileURL, true);
    }

    /**
     * Creates a new MzMLUnmarshaller object from a file
     *
     * @param mzMLFile the file to unmarshall
     */
    public MzMLUnmarshaller(File mzMLFile) {
        this(mzMLFile, true, null);
    }

    /**
     * Creates a new MzMLUnmarshaller object from a URL
     *
     * @param mzMLFileURL       the URL to unmarshall
     * @param aUseSpectrumCache if true the spectra are cached
     */
    public MzMLUnmarshaller(URL mzMLFileURL, boolean aUseSpectrumCache) {
        this(FileUtils.getFileFromURL(mzMLFileURL), aUseSpectrumCache, null);
    }

    /**
     * Creates a new MzMLUnmarshaller object from a file
     *
     * @param mzMLFile          the file to unmarshall
     * @param aUseSpectrumCache if true the spectra are cached
     */
    public MzMLUnmarshaller(File mzMLFile, boolean aUseSpectrumCache, MzMLObjectCache cache) {
        this.mzMLFile = mzMLFile;
        index = MzMLIndexerFactory.getInstance().buildIndex(mzMLFile);
        useSpectrumCache = aUseSpectrumCache;
        this.cache = cache;
    }

    /**
     * USE WITH CAUTION - This will unmarshall a complete MzML object and
     * will likely cause an OutOfMemoryError for very large files.
     *
     * @return an MzML object
     */
    public MzML unmarshall() {
        return unmarshalFromXpath("", MzML.class);
    }

    /**
     * Returns the mzML version.
     *
     * @return the mzML version, null if not found
     */
    public String getMzMLVersion() {

        Matcher match = VERSION_PATTERN.matcher(index.getMzMLAttributeXMLString());

        if (match.find()) {
            return match.group(1);
        } else {
            return null;
        }
    }

    /**
     * Returns the mzML accession number.
     *
     * @return the mzML accession number, null if not found
     */
    public String getMzMLAccession() {

        Matcher match = AC_PATTERN.matcher(index.getMzMLAttributeXMLString());

        if (match.find()) {
            return match.group(1);
        } else {
            return null;
        }
    }

    /**
     * Returns the mzML ID.
     *
     * @return the mzML ID, null if not found
     */
    public String getMzMLId() {

        Matcher match = ID_PATTERN.matcher(index.getMzMLAttributeXMLString());

        if (match.find()) {
            return match.group(1);
        } else {
            return null;
        }
    }

    public Map<String, String> getSingleElementAttributes(String xpath) {
        Map<String, String> attributes = new HashMap<String, String>();
        // retrieve the start tag of the corresponding XML element
        //single Element should appear once in the file
        String tag = index.getStartTag(xpath);
        if (tag == null) {
            return null;
        }

        // parse the tag for attributes
        Matcher match = XML_ATT_PATTERN.matcher(tag);
        while (match.find()) {
            if (match.groupCount() == 2) {
                // found name - value pair
                String name = match.group(1);
                String value = match.group(2);
                // stick the found attributes in the map
                attributes.put(name, value);
            } else {
                // not a name - value pair, something is wrong!
                System.out.println("Unexpected number of groups for XML attribute: " + match.groupCount() + " in tag: " + tag);
            }

        }
        return attributes;
    }

    /**
     * Method to retrieve attribute name-value pairs for a XML element
     * defined by it's id and Class.
     *
     * @param id    the value of the 'id' attribute of the XML element.
     * @param clazz the Class representing the XML element.
     * @return A map of all the found name-value attribute pairs or
     *         null if no element with the specified id was found.
     */
    public Map<String, String> getElementAttributes(String id, Class clazz) {
        Map<String, String> attributes = new HashMap<String, String>();
        // retrieve the start tag of the corresponding XML element
        String tag = index.getStartTag(id, clazz);
        if (tag == null) {
            return null;
        }

        // parse the tag for attributes
        Matcher match = XML_ATT_PATTERN.matcher(tag);
        while (match.find()) {
            if (match.groupCount() == 2) {
                // found name - value pair
                String name = match.group(1);
                String value = match.group(2);
                // stick the found attributes in the map
                attributes.put(name, value);
            } else {
                // not a name - value pair, something is wrong!
                // ToDo: proper handling! exception
                System.out.println("Unexpected number of groups for XML attribute: " + match.groupCount() + " in tag: " + tag);
            }

        }
        return attributes;
    }


    /**
     * Returns the number of elements for a given path.
     *
     * @param xpath the path to look up
     * @return the number of elements for a given path
     */
    public int getObjectCountForXpath(String xpath) {
        return index.getCount(xpath);
    }

    /**
     * Retrieves the list of elements of the given class at the selected path.
     * <p/>
     * Exp.: CVList cvList = unmarshaller.unmarshallFromXPath("/cvList", CVList.class);
     * Retrieves the cvList from element of the mzML file, given it's XPath
     *
     * @param <T>
     * @param xpath the path to search
     * @param cls   the class type to retrieve
     * @return the list of elements of the given class at the selected path
     */
    public <T extends MzMLObject> T unmarshalFromXpath(String xpath, Class cls) {
        // ToDo: only unmarshalls first element in xxindex!! Document this!
        T retval = null;
        try {
            //we want to unmarshal the whole file
            if (xpath.equals("")) {
                xpath = MzMLElement.MzML.getXpath();
                if (isIndexedmzML()) {
                    xpath = MzMLElement.IndexedmzML.getXpath().concat(xpath);
                }

            }
            Iterator<String> xpathIter = index.getXmlStringIterator(xpath);

            if (xpathIter.hasNext()) {

                String xmlSt = xpathIter.next();

                //need to clean up XML to ensure that there are no weird control characters
                String cleanXML = EscapingXMLUtilities.escapeCharacters(xmlSt);

                if (logger.isDebugEnabled()) {
                    logger.trace("XML to unmarshal: " + cleanXML);
                }

                //required for the addition of namespaces to top-level objects
                MzMLNamespaceFilter xmlFilter = new MzMLNamespaceFilter();
                //initializeUnmarshaller will assign the proper reader to the xmlFilter
                Unmarshaller unmarshaller = UnmarshallerFactory.getInstance().initializeUnmarshaller(index, xmlFilter, cache, useSpectrumCache);
                //unmarshall the desired object
                JAXBElement<T> holder = unmarshaller.unmarshal(new SAXSource(xmlFilter, new InputSource(new StringReader(cleanXML))), cls);
                retval = holder.getValue();

                if (logger.isDebugEnabled()) {
                    logger.debug("unmarshalled object = " + retval);
                }
            }

        } catch (JAXBException e) {
            logger.error("MzMLUnmarshaller.unmarshalFromXpath", e);
            throw new IllegalStateException("Could not unmarshal object at xpath:" + xpath);
        }

        return retval;
    }

    /**
     * Retrieves a collection of elements of the given class at the selected path
     *
     * @param <T>
     * @param xpath the path to search
     * @param cls   the class type to retrieve
     * @return the collection of elements of the given class at the selected path
     */
    public <T extends MzMLObject> MzMLObjectIterator<T> unmarshalCollectionFromXpath(String xpath, Class cls) {
        return new MzMLObjectIterator<T>(xpath, cls, index, cache, useSpectrumCache);
    }


    ///// ///// ///// ///// ///// ///// ///// ///// ///// //////
    // additional unmarshal operations for indexedmzML

    // ToDo: add schema validation step or implicit validation with the marshaller/unmarshaller

    /**
     * Returns true of the mzML file is indexed.
     *
     * @return true of the mzML file is indexed
     */
    public boolean isIndexedmzML() {
        // ToDo: find better way to check this?
        // ToDo: maybe change log level in StandardXpathAccess class
        // this check will log an ERROR if it is not an indexedmzML file, since we
        // are trying to retrieve an entry that will not be in the XML
        Iterator iter = index.getXmlStringIterator("/indexedmzML/indexList");
        return iter.hasNext();
    }

    /**
     * Returns true if the mzML file's check sum is ok.
     *
     * @return true if the mzML file's check sum is ok
     * @throws MzMLUnmarshallerException
     */
    public boolean isOkFileChecksum() throws MzMLUnmarshallerException {
        // if we already have established that the checksum has changed, then don't check again
        if (fileCorrupted) {
            return false;
        }

        // if it is not even an indexedmzML, then we throw an exception right away
        if (!isIndexedmzML()) {
            throw new MzMLUnmarshallerException("Attempted check of file checksum on un-indexed mzML file.");
        }

        // ok, now compare the two checksums (provided and calculated)
        String indexChecksum = getFileChecksumFromIndex();
        logger.info("provided checksum (index)  : " + indexChecksum);
        String calcChecksum = calculateChecksum();
        logger.info("calculated checksum (jmzml): " + calcChecksum);
        boolean checkSumOK = indexChecksum.equals(calcChecksum);
//        boolean checkSumOK = true;

        // if the checksums don't match, mark the file as corrupted
        if (!checkSumOK) {
            fileCorrupted = true;
        }

        return checkSumOK;
    }

    /**
     * Returns the mzML index.
     *
     * @return the mzML index
     * @throws MzMLUnmarshallerException
     */
    public IndexList getMzMLIndex() throws MzMLUnmarshallerException {
        IndexList retval;
        // check if already cached
        if (indexList == null) {
            // not yet cached, so we have to unmarshal it
            if (isOkFileChecksum()) {
                retval = unmarshalFromXpath("/indexedmzML/indexList", IndexList.class);
                indexList = retval; // save, so we don't have to generate it again
            } else {
                throw new MzMLUnmarshallerException("File checksum did not match! This file has been changed after the index was created. The index is invalid.");
            }
        } else {
            retval = indexList;
        }

        return retval;
    }

    /**
     * Returns the spectrum corresponding to the provided spectrum ID.
     *
     * @param aID the ID of the spectrum to get
     * @return the spectrum corresponding to the provided spectrum ID, null if no matching spectrum is found
     * @throws MzMLUnmarshallerException
     */
    public Spectrum getSpectrumById(String aID) throws MzMLUnmarshallerException {
        Spectrum result = null;
        String xml = index.getXmlString(aID, Spectrum.class);
        try {
            //need to clean up XML to ensure that there are no weird control characters
            String cleanXML = EscapingXMLUtilities.escapeCharacters(xml);
            //required for the addition of namespaces to top-level objects
            MzMLNamespaceFilter xmlFilter = new MzMLNamespaceFilter();
            //initializeUnmarshaller will assign the proper reader to the xmlFilter
            Unmarshaller unmarshaller = UnmarshallerFactory.getInstance().initializeUnmarshaller(index, xmlFilter, cache, useSpectrumCache);
            //unmarshall the desired object
            JAXBElement<Spectrum> holder = unmarshaller.unmarshal(new SAXSource(xmlFilter, new InputSource(new StringReader(cleanXML))), Spectrum.class);
            result = holder.getValue();
        } catch (JAXBException je) {
            logger.error("MzMLUnmarshaller.getSpectrumByID", je);
            throw new IllegalStateException("Could not unmarshal spectrum with ID: " + aID);
        }
        return result;
    }

    /**
     * Returns the chromatogram corresponding to the provided chromatogram ID.
     *
     * @param aID the ID of the chromatogram to get
     * @return the chromatogram corresponding to the provided chromatogram ID, null if no matching chromatogram is found
     * @throws MzMLUnmarshallerException
     */
    public Chromatogram getChromatogramById(String aID) throws MzMLUnmarshallerException {
        Chromatogram result = null;
        String xml = index.getXmlString(aID, Chromatogram.class);
        try {
            //need to clean up XML to ensure that there are no weird control characters
            String cleanXML = EscapingXMLUtilities.escapeCharacters(xml);
            //required for the addition of namespaces to top-level objects
            MzMLNamespaceFilter xmlFilter = new MzMLNamespaceFilter();
            //initializeUnmarshaller will assign the proper reader to the xmlFilter
            Unmarshaller unmarshaller = UnmarshallerFactory.getInstance().initializeUnmarshaller(index, xmlFilter, cache, useSpectrumCache);
            //unmarshall the desired object
            JAXBElement<Chromatogram> holder = unmarshaller.unmarshal(new SAXSource(xmlFilter, new InputSource(new StringReader(cleanXML))), Chromatogram.class);
            result = holder.getValue();
        } catch (JAXBException je) {
            logger.error("MzMLUnmarshaller.getChromatogramByID", je);
            throw new IllegalStateException("Could not unmarshal chromatogram with ID: " + aID);
        }
        return result;
    }

    /**
     * Returns the spectrum corresponding to a given refId.
     *
     * @param refId the refId of the spectrum to retrieve
     * @return the corresponding spectrum, or null if no spectrum is found
     * @throws MzMLUnmarshallerException
     */
    public Spectrum getSpectrumByRefId(String refId) throws MzMLUnmarshallerException {
        // get the index entry for 'spectrum'
        Index aIndexEntry = getIndex("spectrum");

        // find the offset for the specified refId
        for (Offset offset : aIndexEntry.getOffset()) {
            if (offset.getIdRef().equalsIgnoreCase(refId)) {
                return getElementByOffset("spectrum", offset.getValue());
            }
        }

        return null;
    }

    /**
     * Returns the spectrum with a given spotId.
     *
     * @param spotId the spotId of the spectrum to retrieve
     * @return the corresponding spectrum, or null if no spectrum is found
     * @throws MzMLUnmarshallerException
     */
    public Spectrum getSpectrumBySpotId(String spotId) throws MzMLUnmarshallerException {
        // get the index entry for 'chromatogram'
        Index aIndexEntry = getIndex("spectrum");

        // find the offset for the specified spotId
        for (Offset offset : aIndexEntry.getOffset()) {
            if (offset.getSpotID() != null && offset.getSpotID().equalsIgnoreCase(spotId)) {
                return getElementByOffset("spectrum", offset.getValue());
            }
        }

        return null;
    }

    /**
     * Returns the spectrum with a given scan time.
     *
     * @param scanTime the scan time for the wanted spectrum
     * @return the spectrum with a given scan time, null of no spectrum is found
     * @throws MzMLUnmarshallerException
     */
    public Spectrum getSpectrumByScanTime(double scanTime) throws MzMLUnmarshallerException {
        // get the index entry for 'chromatogram'
        Index aIndexEntry = getIndex("spectrum");

        // find the offset for the specified scanTime
        for (Offset offset : aIndexEntry.getOffset()) {
            if (offset.getScanTime() != null && offset.getScanTime() == scanTime) {
                return getElementByOffset("spectrum", offset.getValue());
            }
        }

        return null;
    }


    /**
     * Returns the spectrum with a given index.
     *
     * @param aIndex Integer with the index for the desired spectrum
     * @return the spectrum with the given index, or 'null' if none is found.
     * @throws MzMLUnmarshallerException
     */
    public Spectrum getSpectrumByScanTime(Integer aIndex) throws MzMLUnmarshallerException {
        // Resolve the index to an ID.
        String specID = index.getSpectrumIDFromSpectrumIndex(aIndex);

        return this.getSpectrumById(specID);
    }

    /**
     * Returns the chromatogram corresponding to a given refId.
     *
     * @param refId the refId of the chromatogram to retrieve
     * @return the chromatogram corresponding to a given refId, null if no chromatogram found
     * @throws MzMLUnmarshallerException
     */
    public Chromatogram getChromatogramByRefId(String refId) throws MzMLUnmarshallerException {
        // get the index entry for 'chromatogram'
        Index aIndexEntry = getIndex("chromatogram");

        // find the offset for the specified refId
        for (Offset offset : aIndexEntry.getOffset()) {
            // we are only interested in a particular refId
            if (offset.getIdRef().equalsIgnoreCase(refId)) {
                return getElementByOffset("chromatogram", offset.getValue());
            }
        }

        return null;
    }


    /**
     * Returns a set containing all spectrum IDs
     *
     * @return a set containing all spectrum IDs
     */
    public Set<String> getSpectrumIDs() {
        return this.index.getSpectrumIDs();
    }

    /**
     * Returns a set containing all spectrum indexes
     *
     * @return a set containing all spectrum indexes
     */
    public Set<Integer> getSpectrumIndexes() {
        return this.index.getSpectrumIndexes();
    }

    /**
     * This method returns the spectrum ID for a given spectrum index, or 'null'
     * if the specified index could not be found.
     *
     * @param aIndex Integer with the spectrum index to retrieve
     *               the spectrum ID for
     * @return String  with the spectrum ID, or 'null' if the index could not be found.
     */
    public String getSpectrumIDFromSpectrumIndex(Integer aIndex) {
        return index.getSpectrumIDFromSpectrumIndex(aIndex);
    }

    /**
     * Returns a set containing all chromatogram IDs
     *
     * @return a set containing all chromatogram IDs
     */
    public Set<String> getChromatogramIDs() {
        return this.index.getChromatogramIDs();
    }

    ///// ///// ///// ///// ///// ///// ///// ///// ///// //////
    // private helper method primarily for indexedmzML stuff

    /**
     * Returns the file's check sum from the index.
     *
     * @return the file's check sum from the index
     * @throws MzMLUnmarshallerException
     */
    private String getFileChecksumFromIndex() throws MzMLUnmarshallerException {
        // there will only be a fileChecksum tag it is a indexedmzML
        if (!isIndexedmzML()) {
            throw new MzMLUnmarshallerException("Can not retrieve fileChecksum from a non indexed mzML file!");
        }

        // now fetch the fileChecksum stored in the indexedmzML
        String checksum;
        Iterator<String> snipIter = index.getXmlStringIterator("/indexedmzML/fileChecksum");
        if (snipIter.hasNext()) {
            String snippet = snipIter.next();
            // we need to cut of the start and stop tag
//            checksum = snippet.substring(14, snippet.length()-15).intern();
            String test = snippet.replace("<fileChecksum>", "");
            checksum = test.replace("</fileChecksum>", "").trim().intern();
        } else {
            throw new IllegalStateException("Could not find fileChecksum tag in indexedmzML: " + mzMLFile.getName());
        }

        return checksum;
    }

    /**
     * Calcultes the check sum.
     *
     * @return the check sum as hexidecimal
     */
    private String calculateChecksum() {
        // we have to create the checksum for the mzML file (from its beginning to the
        // end of the fileChecksum start tag).
        // Since this stop location is very near the end of the file, we skip everything
        // until we come within a certain limit of the end of the file
        long limit = mzMLFile.length() - 200L;
        logger.debug("Looking for fileChecksum tag between byte " + limit +
                " and byte " + mzMLFile.length() + " (the end) of the mzML file.");

        // initialize the hash algorithm
        MessageDigest hash;
        try {
            hash = MessageDigest.getInstance("SHA-1");
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException("SHA-1 not recognized as Secure Hash Algorithm.", e);
        }

        // create the input stream that will calculate the checksum
        FileInputStream fis;
        try {
            fis = new FileInputStream(mzMLFile);
        } catch (FileNotFoundException e) {
            throw new IllegalStateException("File " + mzMLFile.getAbsoluteFile() + " could not be found!", e);
        }
        BufferedInputStream bis = new BufferedInputStream(fis);
        DigestInputStream dis = new DigestInputStream(bis, hash);

        // prepare for input stream processing
        // we read through the file until we reach a specified limit before the end of the file
        // from there we populate a buffer with the read bytes (characters) and check if we have
        // already reached the position up to where we have to calculate the hash.
        CircularFifoBuffer bBuf = new CircularFifoBuffer(15);
        long cnt = 0; // counter to keep track of our position
        byte[] b = new byte[1]; // we only read one byte at a time
        try {
            while (dis.read(b) >= 0) {
                bBuf.add(b[0]);
                cnt++;
                // check if we have already reached the last bit of the file, where we have
                // to find the right position to stop (after the 'fileChecksum' start tag)
                if (cnt > limit) {
                    // we should have reached the start of the <fileChecksum> tag,
                    // now we have to find the end
                    String readBuffer = convert2String(bBuf);
                    if (readBuffer.endsWith("<fileChecksum>")) {
                        // we have found the end of the fileChecksum start tag, we have to stop the hash
                        if (b[0] != '>') { // check that we are really at the last character of the tag
                            throw new IllegalStateException("We are not at the end of <fileChecksum> tag!");
                        }
                        break;
                    }
                } // else if not yet near the end of the file, just keep on going
            }
            dis.close();
        } catch (IOException e) {
            throw new IllegalStateException("Could not read from file '" + mzMLFile.getAbsolutePath() +
                    "' while trying ot calculate hash.", e);
        }
        logger.debug("Read over " + cnt + " bytes while calculating the file hash.");

        byte[] bytesDigest = dis.getMessageDigest().digest();

        return asHex(bytesDigest);
    }

    /**
     * TODO: Javadoc missing
     *
     * @param bBuf
     * @return
     */
    private String convert2String(CircularFifoBuffer bBuf) {
        byte[] tmp = new byte[bBuf.size()];
        int tmpCnt = 0;
        for (Object aBBuf : bBuf) {
            tmp[tmpCnt++] = (Byte) aBBuf;
        }
        return new String(tmp);
    }

    /**
     * TODO: Javadoc missing
     *
     * @param buf
     * @return
     */
    public static String asHex(byte[] buf) {
        // from: http://forums.xkcd.com/viewtopic.php?f=11&t=16666&p=553936
        char[] chars = new char[2 * buf.length];
        for (int i = 0; i < buf.length; ++i) {
            chars[2 * i] = HEX_CHARS[(buf[i] & 0xF0) >>> 4];
            chars[2 * i + 1] = HEX_CHARS[buf[i] & 0x0F];
        }
        return new String(chars);
    }

    /**
     * TODO: Javadoc missing
     *
     * @param elementName
     * @return
     * @throws MzMLUnmarshallerException
     */
    private Index getIndex(String elementName) throws MzMLUnmarshallerException {
        IndexList list = getMzMLIndex();

        for (Index entry : list.getIndex()) {
            if (entry.getName().equalsIgnoreCase(elementName)) {
                return entry;
            }
        }

        return null;
    }

    /**
     * TODO: Javadoc missing
     *
     * @param <T>
     * @param elementName
     * @param offset
     * @return
     * @throws MzMLUnmarshallerException
     */
    private <T extends MzMLObject> T getElementByOffset(String elementName, long offset) throws MzMLUnmarshallerException {
        // now check if we can map the elementName to a xpath from the xxindex
        String aXpath = null;
        for (String xxindexPath : index.getXpath()) {
            // we are looking for a xpath that ends in the elementName (e.g. points to
            // an element with the requested name)
            if (xxindexPath.endsWith(elementName)) {
                aXpath = xxindexPath;
            }
        }
        // if we don't have the xpath, then this method has been used incorrectly!
        if (aXpath == null) {
            throw new MzMLUnmarshallerException("Could not find a valid xpath " +
                    "(in xxindex) for the requested mzML index element '" + elementName + "'!");
        }

        // now that we have the xpath to use for the requested element, check if the xxindex
        // contains an element start position that matches the offset of the desired element
        String xmlSnippet = index.getXmlString(aXpath, offset);
        if (xmlSnippet == null) {
            throw new MzMLUnmarshallerException("No element '" + elementName + "' with the specified " +
                    "offset (" + offset + ") could be found (xpath: '" + aXpath + "')! Perhaps the " +
                    "mzML index containing the offset was corrupted.");
        }

        T retval;
        try {
            // ToDo: check this!! try to replace with standard unmarshaller!
            //need to clean up XML to ensure that there are no weird control characters
            String cleanXML = EscapingXMLUtilities.escapeCharacters(xmlSnippet);
            MzMLNamespaceFilter xmlFilter = new MzMLNamespaceFilter();
            // initializeUnmarshaller will assign the proper reader to the xmlFilter
            Unmarshaller unmarshaller = UnmarshallerFactory.getInstance().initializeUnmarshaller(index, xmlFilter, cache, useSpectrumCache);
            // unmarshall the desired object
            Class cls = ModelConstants.getClassForElementName(elementName);
            JAXBElement<T> holder = unmarshaller.unmarshal(new SAXSource(xmlFilter, new InputSource(new StringReader(cleanXML))), cls);
            retval = holder.getValue();
        } catch (JAXBException e) {
            logger.error("MzMLUnmarshaller.getObjectFromXml", e);
            throw new IllegalStateException("Could not unmarshal object from XML string:" + xmlSnippet);
        }

        return retval;
    }

    /**
     * TODO: Javadoc missing
     *
     * @param <T>
     * @param element
     * @return
     * @throws MzMLUnmarshallerException
     */
    public <T extends MzMLObject> T unmarshalFromIndexElement(IndexElement element, Class cls) throws MzMLUnmarshallerException {

        // now that we have the xpath to use for the requested element, check if the xxindex
        // contains an element start position that matches the offset of the desired element
        String xmlSnippet = index.getXmlString(element);

        T retval;
        try {
            // ToDo: check this!! try to replace with standard unmarshaller!
            String cleanXML = EscapingXMLUtilities.escapeCharacters(xmlSnippet);
            MzMLNamespaceFilter xmlFilter = new MzMLNamespaceFilter();
            // initializeUnmarshaller will assign the proper reader to the xmlFilter
            Unmarshaller unmarshaller = UnmarshallerFactory.getInstance().initializeUnmarshaller(index, xmlFilter, cache, useSpectrumCache);
            // unmarshall the desired object
            JAXBElement<T> holder = unmarshaller.unmarshal(new SAXSource(xmlFilter, new InputSource(new StringReader(cleanXML))), cls);
            retval = holder.getValue();
        } catch (JAXBException e) {
            logger.error("MzMLUnmarshaller.getObjectFromXml", e);
            throw new IllegalStateException("Could not unmarshal object from XML string:" + xmlSnippet);
        }

        return retval;
    }

    /**
     * Returns the mzML XXIndex Wrapper for raw access. This is usually a developer-level method.
     *
     * @return the mzML XXIndex Wrapper for raw acces
     */
    public MzMLIndexer getMzMLIndexer() {
        return index;
    }
}
