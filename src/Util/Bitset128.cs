using System.Numerics;
using System.Text.Json.Serialization;

namespace ServerMain.Util
{
    public struct Bitset128
    {
        [JsonInclude]
        public ulong V0;
        [JsonInclude]
        public ulong V1;

        public Bitset128(ulong v0, ulong v1)
        {
            V0 = v0;
            V1 = v1;
        }

        public int Num()
        {
            return BitOperations.PopCount(V0) + BitOperations.PopCount(V1);
        }

        public IEnumerable<int> Bits()
        {
            for (int pos = 0; pos < 64; pos++)
            {
                if ((V0 & (1UL << pos)) != 0)
                {
                    yield return pos;
                }
            }
            for (int pos = 0; pos < 64; pos++)
            {
                if ((V1 & (1UL << pos)) != 0)
                {
                    yield return 64 + pos;
                }
            }
        }

        public void Pack(IEnumerable<int> indices)
        {
            foreach (var n in indices)
            {
                if (n < 64)
                {
                    V0 |= (1UL << n);
                }
                else if (n < 128)
                {
                    V1 |= (1UL << (n - 64));
                }
            }
        }
    }
}
