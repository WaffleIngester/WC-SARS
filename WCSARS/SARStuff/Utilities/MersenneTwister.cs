using System;

namespace SARStuff
{
    // Please see: http://www.math.sci.hiroshima-u.ac.jp/m-mat/MT/VERSIONS/C-LANG/980409/mt19937-2.c
    public class MersenneTwister // Was this morally wrong?
    {
        // Mersenne Twister Stuff....
        /*
        private const int N = 624;
        private const int M = 397;
        private const uint MATRIX_A = 2567483615U;
        private const uint UPPER_MASK = 2147483648U;
        private const uint LOWER_MASK = 2147483647U;
        private const uint TEMPERING_MASK_B = 2636928640U;
        private const uint TEMPERING_MASK_C = 4022730752U;
        private uint _seed;
        */
        private uint[] mt = new uint[624];
        private short mti;
        private static uint[] mag01 = new uint[] { 0U, 2567483615U };

        public MersenneTwister(uint seed)
        {
            mt[0] = seed & uint.MaxValue;
            for (mti = 1; mti < 624; mti++)
            {
                mt[mti] = 69069U * mt[mti - 1] & uint.MaxValue;
            }
        }

        // Methods
        #region tempering params
        private static uint TEMPER_SHIFT_L(uint y)
        {
            return y >> 18;
        }
        private static uint TEMPER_SHIFT_U(uint y)
        {
            return y >> 11;
        }
        private static uint TEMPER_SHIFT_S(uint y)
        {
            return y << 7;
        }
        private static uint TEMPER_SHIFT_T(uint y)
        {
            return y << 15;
        }
        #endregion tempering params

        protected uint GenerateUInt()
        {
            // Check if we're at the end of the MT. If so, it's time to mix things up once again.
            uint retValue; // While `retValue` is used here; The value it gets set to here is only for mixing up the array.
            if (mti >= 624)
            {
                short index;
                for (index = 0; index < 227; index++)
                {
                    retValue = (mt[index] & 2147483648U) | (mt[index + 1] & 2147483647U);
                    mt[index] = mt[index + 397] ^ retValue >> 1 ^ mag01[(int)(retValue & 1U)];
                }
                while (index < 623) // Now go over the remaining entries...
                {
                    retValue = (mt[index] & 2147483648U) | (mt[index + 1] & 2147483647U);
                    mt[index] = mt[index + -227] ^ retValue >> 1 ^ mag01[(int)(retValue & 1U)];
                    index += 1;
                }
                retValue = (mt[623] & 2147483648U) | (mt[0] & 2147483647U);
                mt[623] = mt[396] ^ retValue >> 1 ^ mag01[(int)(retValue & 1U)];
                mti = 0; // Reset the MersenneTwister-Index back to 0
            }
            // Actually get and return a number
            retValue = mt[mti];
            mti++;
                // XOR, SHIFT, AND; before returning...
            retValue ^= TEMPER_SHIFT_U(retValue);
            retValue ^= TEMPER_SHIFT_S(retValue) & 2636928640U;
            retValue ^= TEMPER_SHIFT_T(retValue) & 4022730752U;
            return retValue ^ TEMPER_SHIFT_L(retValue);
        }

        // There are indeed variants which generate other value-types. However, the only one of interest for loot generaton and most others is this one.
        // Considering how most information as to how all this works was obtained, doing so would be trivial.
        public virtual uint NextUInt(uint minValue, uint maxValue)
        {
            if (minValue < maxValue) return GenerateUInt() / (uint.MaxValue / (maxValue - minValue)) + minValue; // uint.MaxValue == 4294967295 ~= 4294967295.0
            if (minValue == maxValue) return minValue;
            throw new ArgumentOutOfRangeException("minValue", "NextUInt() called with minValue > maxValue");
        }

        /// <summary>
        /// Generates a pseudo-random integer between minValue and maxValue exclusively (i.e., maxValue is never generated).
        /// </summary>
        /// <param name="minValue">The minimum value that can be generated.</param>
        /// <param name="maxValue">The maximum value that can be generated.</param>
        /// <returns>A psuedo-randomly generated value between the desired values.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int NextInt(int minValue, int maxValue)
        {
            if (minValue < maxValue) return (int)(GenerateUInt() / (uint.MaxValue / (maxValue - minValue)) + minValue);
            if (minValue == maxValue) return minValue;
            throw new ArgumentOutOfRangeException("minValue", "Next() called with minValue > maxValue");
        }

        /// <summary>
        /// Generates a pseudo-random integer between minValue and maxValue inclusively (i.e., maxValue can potentially be generated).
        /// </summary>
        /// <param name="minValue">The minimum value that can be generate.</param>
        /// <param name="maxValue">The maximum value that can be generated.</param>
        /// <returns>A psuedo-randomly generated value between the desired points.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int NextInt2(int minValue, int maxValue)
        {
            if (minValue < maxValue) return (int)(GenerateUInt() / (uint.MaxValue / ((maxValue + 1) - minValue)) + minValue);
            if (minValue == maxValue) return minValue;
            throw new ArgumentOutOfRangeException("minValue", "Next() called with minValue > maxValue");
        }
    }
}
