package uk.ac.ebi.jmzml.xml.xxindex;

import org.apache.log4j.Logger;
import psidev.psi.tools.xxindex.SimpleXmlElementExtractor;
import psidev.psi.tools.xxindex.StandardXpathAccess;
import psidev.psi.tools.xxindex.XmlElementExtractor;
import psidev.psi.tools.xxindex.index.IndexElement;
import psidev.psi.tools.xxindex.index.XpathIndex;
import uk.ac.ebi.jmzml.MzMLElement;
import uk.ac.ebi.jmzml.model.mzml.Chromatogram;
import uk.ac.ebi.jmzml.model.mzml.Spectrum;
import uk.ac.ebi.jmzml.xml.Constants;

import java.io.File;
import java.io.IOException;
import java.util.*;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * User: rcote
 * Date: 11-Jun-2008
 * Time: 17:09:40
 * $Id: $
 */
public class MzMLIndexerFactory {

    private static final Logger logger = Logger.getLogger(MzMLIndexerFactory.class);

    private static final MzMLIndexerFactory instance = new MzMLIndexerFactory();
    private static final Pattern ID_PATTERN = Pattern.compile("\\sid\\s*=\\s*['\"]([^'\"]*)['\"]", Pattern.CASE_INSENSITIVE);
    private static final Pattern INDEX_PATTERN = Pattern.compile("\\sindex\\s*=\\s*['\"]([^'\"]*)['\"]", Pattern.CASE_INSENSITIVE);

    private MzMLIndexerFactory() {
    }

    public static MzMLIndexerFactory getInstance() {
        return instance;
    }

    public MzMLIndexer buildIndex(File xmlFile) {
        return new MzMlIndexerImpl(xmlFile);
    }

    private class MzMlIndexerImpl implements MzMLIndexer {

        private File xmlFile = null;
        private StandardXpathAccess xpathAccess = null;
        private XmlElementExtractor xmlExtractor = null;
        private XpathIndex index = null;
        private String root = null;
        private String mzMLAttributeXMLString = null;
        // a unified cache of all the id maps
        private HashMap<Class, LinkedHashMap<String, IndexElement>> idMapCache = new HashMap<Class, LinkedHashMap<String, IndexElement>>();
        private HashMap<Integer, String> spectrumIndexToIDMap = new HashMap<Integer, String>();

        ///// ///// ///// ///// ///// ///// ///// ///// ///// /////
        // Constructor

        private MzMlIndexerImpl(File xmlFile) {

            if (xmlFile == null) {
                throw new IllegalStateException("XML File to index must not be null");
            }
            if (!xmlFile.exists()) {
                throw new IllegalStateException("XML File to index does not exist: " + xmlFile.getAbsolutePath());
            }

            //store file reference
            this.xmlFile = xmlFile;

            try {
                // generate XXINDEX
                logger.info("Creating index: ");
                xpathAccess = new StandardXpathAccess(xmlFile, Constants.XML_INDEXED_XPATHS);
                logger.debug("done!");

                // create xml element extractor
                xmlExtractor = new SimpleXmlElementExtractor();
                String encoding = xmlExtractor.detectFileEncoding(xmlFile.toURI().toURL());
                if (encoding != null){
                    xmlExtractor.setEncoding(encoding);
                }

                // create index
                index = xpathAccess.getIndex();
                root = "/mzML";
                // check if the xxindex contains this root
                if (!index.containsXpath(MzMLElement.MzML.getXpath())) {
                    // if not contained in the xxindex, then maybe we have a indexedzmML file
                    if (!index.containsXpath(MzMLElement.IndexedmzML.getXpath())) {
                        logger.info("Index does not contain mzML root! We are not dealing with an mzML file!");
                        throw new IllegalStateException("Index does not contain mzML root!");
                    }
                    root = "/indexedmzML/mzML";
                }

                // initialize the ID maps
                initIdMaps();

                // extract the MzML attributes from the MzML start tag
                mzMLAttributeXMLString = extractMzMLStartTag(xmlFile);

            } catch (IOException e) {
                logger.error("MzMLIndexerFactory$MzMlIndexerImpl.MzMlIndexerImpl", e);
                throw new IllegalStateException("Could not generate MzML index for file: " + xmlFile);
            }

        }

        /**
         * Method to generate and populate ID maps for the XML elements that should be
         * mapped to a unique ID. This will require that these elements are indexes and
         * that they extend the Identifiable class to make sure they have a unique ID.
         *
         * @throws IOException in case of a read error from the underlying XML file.
         * @see uk.ac.ebi.jmzml.MzMLElement
         */
        private void initIdMaps() throws IOException {
            for (MzMLElement element : MzMLElement.values()) {
                // only for elements were a ID map is needed and a xpath is given
                if (element.isIdMapped() && element.isIndexed()) {
                    logger.debug("Initialising ID map for " + element.getClazz().getName());

                    // check if the according class is a sub-class of Identifiable
//                    if (!IdentifiableMzMLObject.class.isAssignableFrom(element.getClazz())) {
////                        throw new IllegalStateException("Attempt to create ID map for not Identifiable element: " + element.getClazz());
//                    }
                    // so far so good, now generate the ID map (if not already present) and populate it
                    LinkedHashMap<String, IndexElement> map = idMapCache.get(element.getClazz());
                    if (map == null) {
                        map = new LinkedHashMap<String, IndexElement>();
                        idMapCache.put(element.getClazz(), map);
                    }
                    initIdMapCache(map, root + checkRoot(element.getXpath()));
                }
            }
        }

        public String getMzMLAttributeXMLString() {
            return mzMLAttributeXMLString;
        }

        private String extractMzMLStartTag(File xmlFile) throws IOException {
            // get start position of the mzML element

            List<IndexElement> ie = index.getElements(root + checkRoot(MzMLElement.MzML.getXpath()));
            // there is only one root
            long startPos = ie.get(0).getStart();

            // get end position of the mzML start tag
            // this is the start position of the next tag (cvList)
            ie = index.getElements(root + checkRoot(MzMLElement.CVList.getXpath()));
            // there will always be one and only one cvList
            long stopPos = ie.get(0).getStart() - 1;

            // get mzML start tag content
            String startTag = xmlExtractor.readString(startPos, stopPos, xmlFile);
            if (startTag != null) {
                //strip newlines that might interfere with later on regex matching
                startTag = startTag.replace("\n", "");
            }
            return startTag;
        }

        public String getStartTag(String xpath) {
            List<IndexElement> elements = index.getElements(root + checkRoot(xpath));
            String tag = "";
            //tag will be unique within the file
            try {
                tag = xpathAccess.getStartTag(elements.get(0));
            } catch (IOException e) {
                // ToDo: proper handling
                e.printStackTrace();
            }
            return tag;
        }


        public String getStartTag(String id, Class clazz) {
            logger.debug("Getting start tag of element with id: " + id + " for class: " + clazz);
            String tag = null;

            Map<String, IndexElement> idMap = idMapCache.get(clazz);
            if (idMap != null) {
                IndexElement element = idMap.get(id);
                if (element != null) {
                    try {
                        tag = xpathAccess.getStartTag(element);
                    } catch (IOException e) {
                        // ToDo: proper handling
                        e.printStackTrace();
                    }
                } else {
                    // ToDo: what if the element exists, but its id was not cached?
                    // ToDo: throw checked exception?
                }
            }
            return tag;
        }


        /**
         * This method initializes the specified ID (and optionally the specified Index) Map(s)
         * for the given XPath. The index map is optional, because although most elements have an ID,
         * only some have an index.
         */
        private void initIdMapCache(HashMap<String, IndexElement> idMap, String xpath) throws IOException {
            List<IndexElement> ranges = index.getElements(xpath);
            for (IndexElement byteRange : ranges) {
                String xml = xpathAccess.getStartTag(byteRange);
                String id = getIdFromRawXML(xml);
                if (id != null) {
                    idMap.put(id, byteRange);
                } else {
                    throw new IllegalStateException("Error initializing ID cache: No id attribute found for element " + xml);
                }
                if (xpath.equalsIgnoreCase(root + checkRoot(MzMLElement.Spectrum.getXpath()))) {
                    Integer index = getIndexFromRawXML(xml);
                    if (index != null) {
                        spectrumIndexToIDMap.put(index, id);
                    }
                }
            }
        }

        private String getIdFromRawXML(String xml) {
            Matcher match = ID_PATTERN.matcher(xml);
            if (match.find()) {
                return match.group(1).intern();
            } else {
                throw new IllegalStateException("Invalid ID in xml: " + xml);
            }
        }

        private Integer getIndexFromRawXML(String xml) {
            Matcher match = INDEX_PATTERN.matcher(xml);
            if (match.find()) {
                String result = match.group(1).intern();
                try {
                    Integer biResult = new Integer(result);
                    return biResult;
                } catch (NumberFormatException nfe) {
                    throw new IllegalStateException("Index attribute could not be parsed into an integer in xml: " + xml);
                }
            } else {
                throw new IllegalStateException("Invalid index in xml: " + xml);
            }
        }

        public Set<String> getSpectrumIDs() {
//            return spectrumIdMap.keySet();
            return idMapCache.get(Spectrum.class).keySet();
        }

        // TODO: do we need those 2 methods ??

        public Set<Integer> getSpectrumIndexes() {
//            return null;
            return spectrumIndexToIDMap.keySet();
        }

        public String getSpectrumIDFromSpectrumIndex(Integer aIndex) {
            return spectrumIndexToIDMap.get(aIndex);
//            return null;
        }

        public Set<String> getChromatogramIDs() {
//            return chromatogramIdMap.keySet();
            return idMapCache.get(Chromatogram.class).keySet();
        }

        public Iterator<String> getXmlStringIterator(String xpathExpression) {
            if (xpathExpression.contains("indexList") || xpathExpression.contains("fileChecksum")) {
                // we can not use the root "mzML", since the mzML index list is outside the mzML!
                return xpathAccess.getXmlSnippetIterator(checkRoot(root + xpathExpression));
            } else {
                // Note: ! root is always the mzML element (even if we are dealing with indexedmzML) !
                return xpathAccess.getXmlSnippetIterator(root + checkRoot(xpathExpression));
            }
        }

        private String checkRoot(String xpathExpression) {
            // since we're appending the root we've already checked, make
            // sure that the xpath doesn't erroneously contain that root

            // get rid of possible '/indexedmzML' root
            String unrootedXpath = xpathExpression;
            if (unrootedXpath.startsWith("/indexedmzML")) {
                unrootedXpath = unrootedXpath.substring("/indexedmzML".length());
                logger.debug("removed /indexedmzML root expression");
            }
            // get rid of possible '/mzML' root
            if (unrootedXpath.startsWith("/mzML")) {
                unrootedXpath = unrootedXpath.substring("/mzML".length());
                logger.debug("removed /mzML root expression");
            }
            return unrootedXpath;
        }
        // ToDo: maybe generify to <T extends IdentifiableMzMLObject>  Class<T>  ??

        public String getXmlString(String ID, Class clazz) {
            logger.debug("Getting cached ID: " + ID + " from cache: " + clazz);

            HashMap<String, IndexElement> idMap = idMapCache.get(clazz);
            IndexElement element = idMap.get(ID);

            String xmlSnippet = null;
            if (element != null) {
                xmlSnippet = readXML(element);
                if (logger.isTraceEnabled()) {
                    logger.trace("Retrieved xml for class " + clazz + " with ID " + ID + ": " + xmlSnippet);
                }
            }
            return xmlSnippet;

        }

        /**
         * Returns an XML string based on an index element
         *
         * @param indexElement
         * @return
         */
        public String getXmlString(IndexElement indexElement) {
            String xmlSnippet = null;
            if (indexElement != null) {
                xmlSnippet = readXML(indexElement);
            }
            return xmlSnippet;
        }

        private String readXML(IndexElement byteRange) {
            return readXML(byteRange, 0);
        }

        private String readXML(IndexElement byteRange, int maxChars) {
            try {
                if (byteRange != null) {
                    long stop; // where we will stop reading
                    long limitedStop = byteRange.getStart() + maxChars; // the potential end-point of reading
                    // if a limit was specified and the XML element length is longer
                    // than the limit, we only read up to the provided limit
                    if (maxChars > 0 && byteRange.getStop() > limitedStop) {
                        stop = limitedStop;
                    } else { // otherwise we will read up to the end of the XML element
                        stop = byteRange.getStop();
                    }
                    return xmlExtractor.readString(byteRange.getStart(), stop, xmlFile);
                } else {
                    throw new IllegalStateException("Attempting to read NULL ByteRange");
                }
            } catch (IOException e) {
                logger.error("MzMLIndexerFactory$MzMlIndexerImpl.readXML", e);
                throw new IllegalStateException("Could not extract XML from file: " + xmlFile);
            }
        }

        /**
         * @param xpathExpression the xpath defining the XML element.
         * @return the number of XML elements matching the xpath or -1
         *         if no elements were found for the specified xpath.
         */
        public int getCount(String xpathExpression) {
            int retval = -1;
            List<IndexElement> tmpList = index.getElements(root + checkRoot(xpathExpression));
            if (tmpList != null) {
                retval = tmpList.size();
            }
            return retval;
        }

        public String getXmlString(String xpath, long offset) {
            String retVal = null;
            List<IndexElement> indexElements = index.getElements(xpath);
            for (IndexElement indexElement : indexElements) {
                if (indexElement.getStart() == offset) {
                    // found what we are looking for
                    try {
                        retVal = xmlExtractor.readString(indexElement.getStart(), indexElement.getStop(), xmlFile);
                    } catch (IOException ioe) {
                        logger.error("MzMLIndexerFactory$MzMlIndexerImpl.getXmlString(xpath, offset)", ioe);
                        throw new IllegalStateException("Could not extract XML from file: " + xmlFile);
                    }
                    break; // there will only be max one element with a specific offset,
                    // but it does not harm to step out of the loop manually
                }
            }
            return retVal;
        }

        // ToDo: find better way. we don't want to expose this!

        public List<IndexElement> getIndexElements(String xpathExpression) {
            return index.getElements(xpathExpression);
        }

        public Set<String> getXpath() {
            return index.getKeys();
        }

    }


}
