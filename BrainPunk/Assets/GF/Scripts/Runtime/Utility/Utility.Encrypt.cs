namespace GF
{
    public static partial class Utility
    {
        public static partial class Encrypt
        {
            public static byte[] XOR(byte[] data,byte key)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] ^= key;
                }

                return data;
            }
        }
    }
}