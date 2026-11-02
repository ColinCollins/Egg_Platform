using System.Text;

namespace GF
{
    public partial class Define
    {
        public class Crypto
        {
            public static byte[] aesKey = Encoding.UTF8.GetBytes("jskloifueyqsdfg1");
            public static byte[] aesIv = Encoding.UTF8.GetBytes("jskloifueyqsdfg2");
            public static byte HKEY = 0xf2;
            public static byte TKEY = 0x9c;
        }
    }
}