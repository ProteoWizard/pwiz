package uk.ac.ebi.jmzml.xml.io;

/**
 * @author Florian Reisinger
 * @since 0.4
 */
public class MzMLUnmarshallerException extends Exception {

    public MzMLUnmarshallerException(String msg) {
        super(msg);
    }

    public MzMLUnmarshallerException(String msg, Throwable cause) {
        super(msg, cause);
    }

}
