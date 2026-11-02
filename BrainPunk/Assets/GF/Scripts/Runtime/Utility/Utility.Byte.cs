namespace GF
{
    public static partial class Utility
    {
        public static partial class Encrypt
        {
            public static byte[] ConcatenateBytes(byte[] array1, byte[] array2)
            {
                byte[] result = new byte[array1.Length + array2.Length];
                System.Buffer.BlockCopy(array1, 0, result, 0, array1.Length);
                System.Buffer.BlockCopy(array2, 0, result, array1.Length, array2.Length);
                return result;
            }
        }
    }
}