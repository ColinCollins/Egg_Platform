// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("5U6/IeCaawbvY9ydefHf4TLKjAaLvqSIYTPRW3wm/4TCjmxygwauTzW2uLeHNba9tTW2trdVg7mcKrB3/C7MBvG4JcdXFPS09bUMB3EZSEjaStBAILg/qIwf5eUdJubWUobd3a7P7BGqsXRxilIXI6bKxECLrSpdKFaxF2h21xabISZaFw81eVVsHaKL4DVYu94xQton9RbKLt6EQoS/OO0mqzuA598e13BuVv552nK8tnUF57SIDOOqG0+kITz/lBV9fZikx6WHNbaVh7qxvp0x/zFAura2trK3tBqOtoO4GM41FRtpeRxj6D20CmF6xX1N/vA72jd+8E5ST/4godj4wuvedtDeohfJUXJa29zENlLteNFv50GCudjNzsk94LW0tre2");
        private static int[] order = new int[] { 4,5,5,11,6,7,11,8,13,12,11,12,13,13,14 };
        private static int key = 183;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
