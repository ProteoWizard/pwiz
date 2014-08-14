package edu.uw.gs.skyline.cloudapi.mzmlharness;

import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.ChromatogramGenerator;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.chromatogramrequest.ChromatogramRequestDocument;
import edu.uw.gs.skyline.cloudapi.chromatogramgenerator.data.MsDataFile;
import junit.framework.TestCase;
import uk.ac.ebi.jmzml.xml.io.MzMLUnmarshaller;

import javax.xml.bind.JAXBContext;
import javax.xml.bind.Unmarshaller;
import java.io.BufferedInputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStream;
import java.io.UnsupportedEncodingException;
import java.net.URI;
import java.net.URISyntaxException;
import java.net.URLEncoder;
import java.text.SimpleDateFormat;
import java.util.Date;

/**
 * Created by nicksh on 3/27/2014.
 */

public class GenerateChorusResponses extends TestCase {
    private static final DataFile[] dataFiles = new DataFile[] {
        new DataFile("Thermo_DDA", "20130311_DDA_Pit01.mzML"),
        new DataFile("Thermo_DIA", "20130311_DIA_Pit01.mzML"),

    };
    private File outputFolder;
    public void testGenerateChorusResponses() throws Exception {
        outputFolder = getTestOutputDirectory();
        for (DataFile dataFile : dataFiles) {
            generateChorusResponse(dataFile);
        }
    }

    private void generateChorusResponse(DataFile dataFile) throws Exception {
        File mzmlFile = downloadMzmlFile(dataFile);
        File outputFile = new File(outputFolder, dataFile.getDatasetName() + "." + dataFile.getMzmlFileName() + ".chorusresponse");
        Unmarshaller chromatogramRequestUnmarshaller = JAXBContext.newInstance(ChromatogramRequestDocument.class)
                .createUnmarshaller();
        ChromatogramRequestDocument chromatogramRequest = (ChromatogramRequestDocument)
                chromatogramRequestUnmarshaller.unmarshal(dataFile.getChorusRequestUri().toURL());
        MzMLUnmarshaller mzmlUnmarshaller = new MzMLUnmarshaller(mzmlFile);
        MsDataFile msDataFile = MzmlProgram.makeMsDataFile(mzmlUnmarshaller, chromatogramRequest);
        FileOutputStream outputStream = null;
        try {
            outputStream = new FileOutputStream(outputFile);
            ChromatogramGenerator.generateChromatograms(msDataFile, chromatogramRequest, outputStream);
        }
        finally {
            if (null != outputStream) {
                outputStream.close();
            }
        }
    }

    private File downloadMzmlFile(DataFile dataFile) throws Exception {
        File downloadsFolder = findDownloadsDirectory();
        File mzmlFile = new File(downloadsFolder, dataFile.getMzmlFileName());
        if (!mzmlFile.exists()) {
            File tmpFile = File.createTempFile("dwn", "tmp", downloadsFolder);
            downloadUriToFile(dataFile.getMzmlFileUri(), tmpFile);
            tmpFile.renameTo(mzmlFile);
        }
        return mzmlFile;
    }

    private File findDownloadsDirectory() {
        File downloadsFolder = new File(findTestResultsDirectory(), "Downloads");
        if (!downloadsFolder.exists()) {
            downloadsFolder.mkdir();
        }
        return downloadsFolder;
    }

    private File getTestOutputDirectory() {
        File outputDirectory = new File(findTestResultsDirectory(), "Output");
        if (!outputDirectory.exists()) {
            outputDirectory.mkdir();
        }
        Date now = new Date();
        SimpleDateFormat dateFormat = new SimpleDateFormat("yyyy-MM-dd HH-mm-ss");
        File testOutputDirectory = new File(outputDirectory, dateFormat.format(now));
        testOutputDirectory.mkdir();
        return testOutputDirectory;
    }

    private File findTestResultsDirectory() {
        for (File file = new File("."); file != null; file = file.getParentFile()) {
            File fileDownloads = new File(file, "TestResults");
            if (fileDownloads.exists() && fileDownloads.isDirectory()) {
                return fileDownloads;
            }
            if (file.equals(file.getParentFile())) {
                break;
            }
        }
        throw new IllegalStateException("Could not find TestResults directory");
    }

    private void downloadUriToFile(URI uri, File file) throws Exception {
        OutputStream out = null;
        try {
            out = new FileOutputStream(file);
            BufferedInputStream in = new BufferedInputStream(uri.toURL().openStream());
            byte data[] = new byte[65536];
            int count;
            while((count = in.read(data,0,data.length)) != -1)
            {
                out.write(data, 0, count);
            }
        } finally {
            if (null != out) {
                out.close();
            }
        }
    }

    static class DataFile {
        String datasetName;
        String mzmlFileName;

        public DataFile(String datasetName, String mzmlFileName) {
            this.datasetName = datasetName;
            this.mzmlFileName = mzmlFileName;
        }

        public String getDatasetName() {
            return datasetName;
        }
        public String getMzmlFileName() {
            return mzmlFileName;
        }
        public URI getChorusRequestUri() {
            return makeUri("http://proteome.gs.washington.edu/~nicksh/chorus/chorusrequests/" + encodeUrl(getDatasetName()) + ".chorusrequest.xml");
        }
        public URI getMzmlFileUri() {
            return makeUri("http://proteome.gs.washington.edu/~nicksh/chorus/mzmlfiles/" + encodeUrl(getMzmlFileName()));
        }
    }

    static String encodeUrl(String str) {
        try {
            return URLEncoder.encode(str, "UTF-8");
        } catch (UnsupportedEncodingException uee) {
            throw new RuntimeException(uee);
        }
    }

    static URI makeUri(String str) {
        try {
            return new URI(str);
        } catch (URISyntaxException use) {
            throw new RuntimeException(use);
        }
    }
}
