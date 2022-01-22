namespace PngParser
{
    public static class Helpers
    {
        public static bool CompareBytes(in byte[] first, in byte[] second)
        {
            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                    return false;
            }

            return true;
        }

        public static byte[] ReverseBytes(in byte[] bytes)
        {
            byte[] reversedBytes = new byte[bytes.Length];

            for (int j = 0, i = bytes.Length - 1; i > -1; i--, j++)
            {
                reversedBytes[j] = bytes[i];
            }

            return reversedBytes;
        }
    }
}