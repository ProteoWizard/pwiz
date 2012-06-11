Updating the Thermo MS FileReader is tricky due to legal restrictions Thermo has regarding
redistribution of their DLLs.

The following instructions describe the steps.

1. Download latest version of MSFileReader.  In theory, this URL should change for every
release, so the best way to find it is with Google:
    https://www.google.com/search?q=Thermo+MSFilereader
Look for a result titled "Thermo Electron Corporation -- Customer Download" with the most
recent date.

2. Run installer MSFileReader.exe

3. Install "MS FileReader for 32 Bit"  (64 bit soon!).  The build expects it to be
installed in the default directory (C:\Program Files (x86)\Thermo).

4. Create a new Console Application in Visual Studio

5. Add a reference to MSFileReaderLib from C:\Program Files (x86)\Thermo\XRawfile2.dll

6. Edit the properties of MSFileReaderLib and change the "Isolated" property to True.

7. Edit the manifest for your console application (something like
"ConsoleApplication.exe.manifest")

8. Copy the <dsig:DigestValue> tag and the hash string it contains (something like
"<dsig:DigestValue>DASQHtRmDlEr/SJrntIZ+Z+KbHk=</dsig:DigestValue>")

9. Edit the side-by-side manifest for MSFileReader at 
pwiz\pwiz_aux\msrc\utility\vendor_api\thermo\MSFileReader.XRawfile2.SxS.manifest.real

10. Replace the <dsig:DigestValue> tag and its contents with the one you copied from the
console application manifest above.

11. Get the version number of XRawfile2.dll: right-click on XRawfile2.dll in the install
directory, open its properties, click on the Details tab, and write down the File version.

12. In the side-by-side manifest (MSFileReader.XRawfile2.SxS.manifest.real), replace the
version attribute of the assembly tag with the File version you saw in the previous step
(something like version="2.2.61.0")

13. Now you should be ready to run quickbuild.
