
package edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest;

import javax.xml.bind.annotation.XmlRegistry;


/**
 * This object contains factory methods for each 
 * Java content interface and Java element interface 
 * generated in the edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest package. 
 * <p>An ObjectFactory allows you to programatically 
 * construct new instances of the Java representation 
 * for XML content. The Java representation of XML 
 * content can consist of schema derived interfaces 
 * and classes representing the binding of schema 
 * type definitions, element declarations and model 
 * groups.  Factory methods for each of these are 
 * provided in this class.
 * 
 */
@XmlRegistry
public class ObjectFactory {


    /**
     * Create a new ObjectFactory that can be used to create new instances of schema derived classes for package: edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest
     * 
     */
    public ObjectFactory() {
    }

    /**
     * Create an instance of {@link ChromatogramRequestDocument }
     * 
     */
    public ChromatogramRequestDocument createChromatogramRequestDocument() {
        return new ChromatogramRequestDocument();
    }

    /**
     * Create an instance of {@link ChromatogramRequestDocument.ChromatogramGroup }
     * 
     */
    public ChromatogramRequestDocument.ChromatogramGroup createChromatogramRequestDocumentChromatogramGroup() {
        return new ChromatogramRequestDocument.ChromatogramGroup();
    }

    /**
     * Create an instance of {@link ChromatogramRequestDocument.IsolationScheme }
     * 
     */
    public ChromatogramRequestDocument.IsolationScheme createChromatogramRequestDocumentIsolationScheme() {
        return new ChromatogramRequestDocument.IsolationScheme();
    }

    /**
     * Create an instance of {@link ChromatogramRequestDocument.ChromatogramGroup.Chromatogram }
     * 
     */
    public ChromatogramRequestDocument.ChromatogramGroup.Chromatogram createChromatogramRequestDocumentChromatogramGroupChromatogram() {
        return new ChromatogramRequestDocument.ChromatogramGroup.Chromatogram();
    }

    /**
     * Create an instance of {@link ChromatogramRequestDocument.IsolationScheme.IsolationWindow }
     * 
     */
    public ChromatogramRequestDocument.IsolationScheme.IsolationWindow createChromatogramRequestDocumentIsolationSchemeIsolationWindow() {
        return new ChromatogramRequestDocument.IsolationScheme.IsolationWindow();
    }

}
