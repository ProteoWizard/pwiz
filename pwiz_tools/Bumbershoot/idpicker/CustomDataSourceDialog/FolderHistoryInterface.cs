//
// $Id: TableExporter.cs 287 2011-08-05 16:41:22Z holmanjd $
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s): 
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomDataSourceDialog
{
    static public class FolderHistoryInterface
    {
        static public List<string> GetRecentFolders()
        {
            return Properties.Settings.Default.FolderHistory.Split("|".ToCharArray(),StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        static public void AddFolderToHistory(string newFolder)
        {
            var currentList = GetRecentFolders();
            
            //remove old occurance of folder (if it's in there) and add to end
            currentList.Remove(newFolder.Replace('|', '!'));
            currentList.Insert(0, newFolder.Replace('|','!'));

            //remove excess folders
            while (currentList.Count > 24)
                currentList.RemoveAt(currentList.Count-1);

            //save changes
            Properties.Settings.Default.FolderHistory = string.Join("|", currentList.ToArray());
            Properties.Settings.Default.Save();
        }
    }
}
