package uk.ac.ebi.jmzml.xml.util;

import javax.xml.namespace.NamespaceContext;
import javax.xml.stream.XMLStreamException;
import javax.xml.stream.XMLStreamWriter;

/**
 * Delegating {@link javax.xml.stream.XMLStreamWriter} that filters out UTF-8 characters that
 * are illegal in XML.
 * <p/>
 * See forum post: http://glassfish.10926.n7.nabble.com/Escaping-illegal-characters-during-marshalling-td59751.html#a20090044
 *
 * @author Erik van Zijst
 */
public class EscapingXMLStreamWriter implements XMLStreamWriter {

    private final XMLStreamWriter writer;

    public EscapingXMLStreamWriter(XMLStreamWriter writer) {

        if (null == writer) {
            throw new IllegalArgumentException("null");
        } else {
            this.writer = writer;
        }
    }

    public void writeStartElement(String s) throws XMLStreamException {
        writer.writeStartElement(s);
    }

    public void writeStartElement(String s, String s1) throws XMLStreamException {
        writer.writeStartElement(s, s1);
    }

    public void writeStartElement(String s, String s1, String s2)
            throws XMLStreamException {
        writer.writeStartElement(s, s1, s2);
    }

    public void writeEmptyElement(String s, String s1) throws XMLStreamException {
        writer.writeEmptyElement(s, s1);
    }

    public void writeEmptyElement(String s, String s1, String s2)
            throws XMLStreamException {
        writer.writeEmptyElement(s, s1, s2);
    }

    public void writeEmptyElement(String s) throws XMLStreamException {
        writer.writeEmptyElement(s);
    }

    public void writeEndElement() throws XMLStreamException {
        writer.writeEndElement();
    }

    public void writeEndDocument() throws XMLStreamException {
        writer.writeEndDocument();
    }

    public void close() throws XMLStreamException {
        writer.close();
    }

    public void flush() throws XMLStreamException {
        writer.flush();
    }

    public void writeAttribute(String localName, String value) throws XMLStreamException {
        writer.writeAttribute(localName, EscapingXMLUtilities.escapeCharacters(value));
    }

    public void writeAttribute(String prefix, String namespaceUri, String localName, String value)
            throws XMLStreamException {
        writer.writeAttribute(prefix, namespaceUri, localName, EscapingXMLUtilities.escapeCharacters(value));
    }

    public void writeAttribute(String namespaceUri, String localName, String value)
            throws XMLStreamException {
        writer.writeAttribute(namespaceUri, localName, EscapingXMLUtilities.escapeCharacters(value));
    }

    public void writeNamespace(String s, String s1) throws XMLStreamException {
        writer.writeNamespace(s, s1);
    }

    public void writeDefaultNamespace(String s) throws XMLStreamException {
        writer.writeDefaultNamespace(s);
    }

    public void writeComment(String s) throws XMLStreamException {
        writer.writeComment(s);
    }

    public void writeProcessingInstruction(String s) throws XMLStreamException {
        writer.writeProcessingInstruction(s);
    }

    public void writeProcessingInstruction(String s, String s1)
            throws XMLStreamException {
        writer.writeProcessingInstruction(s, s1);
    }

    public void writeCData(String s) throws XMLStreamException {
        writer.writeCData(EscapingXMLUtilities.escapeCharacters(s));
    }

    public void writeDTD(String s) throws XMLStreamException {
        writer.writeDTD(s);
    }

    public void writeEntityRef(String s) throws XMLStreamException {
        writer.writeEntityRef(s);
    }

    public void writeStartDocument() throws XMLStreamException {
        writer.writeStartDocument();
    }

    public void writeStartDocument(String s) throws XMLStreamException {
        writer.writeStartDocument(s);
    }

    public void writeStartDocument(String s, String s1)
            throws XMLStreamException {
        writer.writeStartDocument(s, s1);
    }

    public void writeCharacters(String s) throws XMLStreamException {
        writer.writeCharacters(EscapingXMLUtilities.escapeCharacters(s));
    }

    public void writeCharacters(char[] chars, int start, int len)
            throws XMLStreamException {
        writer.writeCharacters(EscapingXMLUtilities.escapeCharacters(new String(chars, start, len)));
    }

    public String getPrefix(String s) throws XMLStreamException {
        return writer.getPrefix(s);
    }

    public void setPrefix(String s, String s1) throws XMLStreamException {
        writer.setPrefix(s, s1);
    }

    public void setDefaultNamespace(String s) throws XMLStreamException {
        writer.setDefaultNamespace(s);
    }

    public void setNamespaceContext(NamespaceContext namespaceContext)
            throws XMLStreamException {
        writer.setNamespaceContext(namespaceContext);
    }

    public NamespaceContext getNamespaceContext() {
        return writer.getNamespaceContext();
    }

    public Object getProperty(String s) throws IllegalArgumentException {
        return writer.getProperty(s);
    }
}