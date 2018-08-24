namespace pwiz.ProteowizardWrapper
{
    public static class SHA1Calculator
    {
        public static string Hash(string buffer)
        {
            return CLI.util.SHA1CalculatorCLI.Hash(buffer);
        }

        public static string HashFile(string fileName)
        {
            return CLI.util.SHA1CalculatorCLI.HashFile(fileName);
        }
    }
}
