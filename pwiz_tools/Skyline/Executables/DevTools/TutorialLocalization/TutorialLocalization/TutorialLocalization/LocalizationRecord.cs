using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TutorialLocalization
{
    public class LocalizationRecord
    {
        public LocalizationRecord(string tutorialName, string xPath, string english)
        {
            TutorialName = tutorialName;
            XPath = xPath;
            English = english;
        }
        public string TutorialName { get; }
        public string XPath { get; }
        public string English { get; }
        public string Localized { get; }
        public string OriginalEnglish { get; }
    }
}
