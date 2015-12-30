/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.IO;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public static class ImportAssayLibraryHelper
    {
        public static List<DbIrtPeptide> GetUnscoredIrtPeptides(List<DbIrtPeptide> dbIrtPeptides, RCalcIrt calcIrt)
        {
            var dbIrtPeptidesFilter = new List<DbIrtPeptide>();
            // Filter out peptides that have the same sequence and iRT as those in the database
            foreach (var dbIrtPeptide in dbIrtPeptides)
            {
                double? oldScore = calcIrt != null ? calcIrt.ScoreSequence(dbIrtPeptide.PeptideModSeq) : null;
                if (oldScore == null || Math.Abs(oldScore.Value - dbIrtPeptide.Irt) > DbIrtPeptide.IRT_MIN_DIFF)
                {
                    dbIrtPeptidesFilter.Add(dbIrtPeptide);
                }
            }
            return dbIrtPeptidesFilter;
        }

        public static void CreateIrtDatabase(string path)
        {
            FileEx.SafeDelete(path);

            //Create file, initialize db
            try
            {
                IrtDb.CreateIrtDb(path);
            }
            catch (DatabaseOpeningException)
            {
                throw;
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.EditIrtCalcDlg_CreateDatabase_The_file__0__could_not_be_created, path),
                                                    x.Message);
                throw new IOException(message, x);
            }
        }
    }
}
