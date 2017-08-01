using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using CsvHelper;
using System.IO;
using System.Text;

namespace SkyGadget
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
//            Application.EnableVisualStyles();
//            Application.SetCompatibleTextRenderingDefault(false);
//            Application.Run(new Form1());
            if (args.Length == 0)
            {
                Console.WriteLine("Error opening report file from Skyline, try re-installing this tool."); // Not L10N
                return;
            }
            var reportFilePath = args[0];

            Dictionary<string, Dictionary<string, RowData>> rows = new Dictionary<string, Dictionary<string, RowData>>();
            List<string> colHeaders = new List<string>();
            var csv = new CsvReader(File.OpenText(reportFilePath));
            while (csv.Read())
            {
                try
                {
                    var pepSeq = csv.GetField<string>(0);
                    var protName = csv.GetField<string>(1);
                    var repName = csv.GetField<string>(2);
                    var fileName = csv.GetField<string>(3);
                    var peptidePeakFoundRatio = csv.GetField<string>(4);
                    var pepRT = csv.GetField<string>(5);
                    var protPrefName = csv.GetField<string>(6);
                    var accession = csv.GetField<string>(7);
                    var ratioToStandard = csv.GetField<string>(8);
                    var k1 = repName + "," + fileName;

                    // ex. Aat1_MVPIVDMAYQGLESGNLLK(protein:Aat1)(PeptideMVPIVDMAYQGLESGNLLK)(UniProt:Q01802)(Preferred_name:AATM_YEAST)
                    var k2 = protName + "_" + pepSeq + "(protein:" + protName + ")(Peptide" + pepSeq + ")"; // essential
                    if (!NA(accession) && !string.IsNullOrEmpty(accession)) // optional
                        k2 += "(UniProt:" + accession + ")";
                    if (!NA(protPrefName) && ! string.IsNullOrEmpty(protPrefName)) // optional
                        k2 += "(Preferred_name:" + protPrefName + ")";
                    var r = new RowData(pepSeq, protName, repName, fileName, peptidePeakFoundRatio,
                        pepRT, ratioToStandard, accession, protPrefName);
                    if(!rows.ContainsKey(k1))
                        rows.Add(k1, new Dictionary<string, RowData>());

                    if(!rows[k1].ContainsKey(k2))
                        rows[k1][k2] = r;

                    // Add to list of all column headers
                    if(!colHeaders.Contains(k2))
                        colHeaders.Add(k2);
                }
                catch (Exception e)
                {
                    
                }
            }
            colHeaders.Sort();
            using (var saveFileDialog = new SaveFileDialog
            {
                FileName = "TFExport.csv", // Not L10N
                Filter = "csv files (*.csv)|*.csv" // Not L10N
            })
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                var saveFileLocation = saveFileDialog.FileName;
                var res = WriteToCsv(saveFileLocation, rows, colHeaders);
                if (string.IsNullOrEmpty(res))
                {
                    Console.WriteLine("File saved to {0}.", saveFileLocation);
                    try
                    {
                        Process.Start("explorer.exe", saveFileLocation);
                    }
                    catch
                    {
                        // do nothing because the save location is logged to the immdiate window and this is not important enough
                        // to worry much about
                    }
                }
                else // if not success
                {
                    Console.WriteLine("Error: {0}", res);
                }
            }


        }
        private static string WriteToCsv(string saveFileLocation, Dictionary<string, Dictionary<string, RowData>> rows, List<string> colHeaders)
        {
            List<string> csvLines = new List<string>(); // each object is a line that will be outputted when the result csv file is saved
            string headRow = "Sample Name, Data Name";
            foreach (var s in colHeaders)
            {
                headRow += "," + s;
            }
            csvLines.Add(headRow);
            foreach (var k in rows.Keys)
            {
                var dict = rows[k];
                string rowString = k;
                foreach (var colHeader in colHeaders)
                {
                    string rts = dict[colHeader].RatioToStandard;
                    if (NA(rts))
                        rowString += ",";
                    else
                        rowString += "," + rts;
                }
                csvLines.Add(rowString);
            }
            WriteCsv(csvLines, saveFileLocation);
            return "";
        }

        private static bool NA(string cell)
        {
            return cell == "#N/A"; // Not L10N
        }

        private static bool WriteCsv(List<string> csvLines, string saveLocation)
        {
            var sb = new StringBuilder();
            foreach (var line in csvLines)
            {
                sb.Append(line + Environment.NewLine);
            }
            try
            {
                using (StreamWriter outfile =
                    new StreamWriter(
                        saveLocation)
                    )
                {
                    outfile.Write(sb.ToString());
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        class RowData
        {
            private string pepetideSequence;
            private string protName;
            private string repName;
            private string fileName;
            private string peptidePeakFoundR;
            private string pepRT;
            private string ratioToStandard;
            private string accession;
            private string preferredName;

            public RowData(string pepetideSequence, string protName, string repName, string fileName, 
                string peptidePeakFoundR, string pepRt, string ratioToStandard, string accession, string prefName)
            {
                this.pepetideSequence = pepetideSequence;
                this.protName = protName;
                this.repName = repName;
                this.fileName = fileName;
                this.peptidePeakFoundR = peptidePeakFoundR;
                pepRT = pepRt;
                this.ratioToStandard = ratioToStandard;
                this.accession = accession;
                this.preferredName = prefName;
            }

            public string PepetideSequence
            {
                get { return pepetideSequence; }
                set { pepetideSequence = value; }
            }

            public string ProtName
            {
                get { return protName; }
                set { protName = value; }
            }

            public string RepName
            {
                get { return repName; }
                set { repName = value; }
            }

            public string FileName
            {
                get { return fileName; }
                set { fileName = value; }
            }

            public string PeptidePeakFoundR
            {
                get { return peptidePeakFoundR; }
                set { peptidePeakFoundR = value; }
            }

            public string PepRt
            {
                get { return pepRT; }
                set { pepRT = value; }
            }

            public string RatioToStandard
            {
                get { return ratioToStandard; }
                set { ratioToStandard = value; }
            }

            public string Accession
            {
                get { return accession; }
                set { accession = value; }
            }

            public string PreferredName
            {
                get { return preferredName; }
                set { preferredName = value; }
            }
        }
    }
}

