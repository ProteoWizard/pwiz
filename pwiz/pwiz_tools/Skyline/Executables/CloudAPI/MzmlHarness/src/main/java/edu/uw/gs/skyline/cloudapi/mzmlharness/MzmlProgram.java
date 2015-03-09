package edu.uw.gs.skyline.cloudapi.mzmlharness;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.ChromatogramGenerator;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromatogramRequestDocument;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsDataFile;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsSpectrum;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller;

import javax.xml.bind.JAXBContext;
import javax.xml.bind.Unmarshaller;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStream;
import java.net.MalformedURLException;
import java.net.URL;
import java.util.Iterator;

/**
 * Created by nicksh on 3/7/14.
 */
public class MzmlProgram {
    public static void main(String[] args) throws Exception {
        if (args.length < 1 || args.length > 3) {
            System.err.println("Wrong number of arguments " + args.length);
            System.err.println("mzmlFileURL [spectrumFilterDocumentURL [OutputFile]]");
            System.exit(1);
        }
        Unmarshaller spectrumFilterUnmarshaller = JAXBContext.newInstance(ChromatogramRequestDocument.class)
                .createUnmarshaller();
        ChromatogramRequestDocument spectrumFilterDocument;
        MzMLUnmarshaller mzmlUnmarshaller;
        URL url = null;
        try {
            url = new URL(args[0]);
        } catch (MalformedURLException mfe) {
            // ignore
        }
        if (url == null) {
            mzmlUnmarshaller = new MzMLUnmarshaller(new File(args[0]));
        }
        else {
            mzmlUnmarshaller = new MzMLUnmarshaller(url);
        }
        OutputStream outputStream = null;
        if (args.length > 1) {
            spectrumFilterDocument = (ChromatogramRequestDocument) spectrumFilterUnmarshaller.unmarshal(makeURL(args[1]));
            if (args.length > 2) {
                outputStream = new FileOutputStream(new File(args[2]));
            }
        } else {
            spectrumFilterDocument = (ChromatogramRequestDocument) spectrumFilterUnmarshaller.unmarshal(System.in);
        }
        if (null == outputStream) {
            outputStream = System.out;
        }
        MsDataFile msDataFile = makeMsDataFile(mzmlUnmarshaller, spectrumFilterDocument);
        ChromatogramGenerator.generateChromatograms(msDataFile,
                spectrumFilterDocument,
                outputStream == null ? System.out : outputStream);
        if (null != outputStream) {
            outputStream.close();
        }
    }

    public static MsDataFile makeMsDataFile(final MzMLUnmarshaller mzmlUnmarshaller, final ChromatogramRequestDocument chromatogramRequestDocument) {
        MsDataFile msDataFile = new MsDataFile();
        msDataFile.setSpectra(new Iterable<MsSpectrum>() {
            @Override
            public Iterator<MsSpectrum> iterator() {
                return new MzmlSpectrumIterator(mzmlUnmarshaller, chromatogramRequestDocument);
            }
        });
        return msDataFile;
    }

    private static URL makeURL(String value) throws Exception {
        try {
            return new URL(value);
        } catch (MalformedURLException malformedURLException) {
            File file = new File(value);
            return file.toURI().toURL();
        }
    }
}
