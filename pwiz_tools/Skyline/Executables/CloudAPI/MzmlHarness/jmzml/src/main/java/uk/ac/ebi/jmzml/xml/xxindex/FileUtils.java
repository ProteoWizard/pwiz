/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.xxindex.FileUtils
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
 *
 *
 *
 */

package uk.ac.ebi.jmzml.xml.xxindex;

import org.apache.log4j.Logger;

import java.io.*;
import java.net.URL;

public class FileUtils {

    private static Logger logger = Logger.getLogger(FileUtils.class);

    public static File getFileFromURL(URL url) {

        File tempFile;
        BufferedInputStream in = null;
        BufferedOutputStream out = null;
        try {

            String tempDir = System.getProperty("java.io.tmpdir", ".");

            // Create temp file.
            tempFile = File.createTempFile("xxindex", ".tmp", new File(tempDir));

            // Delete temp file when program exits.
            tempFile.deleteOnExit();

            // copy content of URL to local file
            InputStream is = url.openStream();
            in = new BufferedInputStream(is);

            FileOutputStream fos = new FileOutputStream(tempFile);
            out = new BufferedOutputStream(fos);

            byte[] b = new byte[1];
            while (in.read(b) >= 0 ) {
                out.write(b);
            }

            logger.debug(url + " written to local file " + tempFile.getAbsolutePath());


        } catch (IOException e) {
            throw new IllegalStateException("Could not create local file for URL: " + url, e);
        } finally {
            try {
                if (in != null) {
                    in.close();
                }
                if (out != null) {
                    out.close();
                }
            } catch (IOException e) {
                /* ignore */
            }

        }

        return tempFile;
    }

}
