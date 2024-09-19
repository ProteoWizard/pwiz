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
        public string Localized { get; private set; }
        public string OriginalEnglish { get; private set; }

        public LocalizationRecord ChangeOriginalEnglish(string originalEnglish, string originalLocalized)
        {
            var localizationRecord = (LocalizationRecord)MemberwiseClone();
            if (originalEnglish != English)
            {
                localizationRecord.OriginalEnglish = originalEnglish;
            }
            localizationRecord.Localized = originalLocalized;
            return localizationRecord;
        }
    }
}
