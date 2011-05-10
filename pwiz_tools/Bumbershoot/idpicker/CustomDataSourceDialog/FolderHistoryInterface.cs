using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomDataSourceDialog
{
    static class FolderHistoryInterface
    {
        static internal List<string> GetRecentFolders()
        {
            return Properties.Settings.Default.FolderHistory.Split('|').ToList();
        }

        static internal void AddFolderToHistory(string newFolder)
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
