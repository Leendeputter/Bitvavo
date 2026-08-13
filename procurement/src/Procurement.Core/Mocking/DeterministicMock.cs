using System.Text;

namespace Procurement.Core.Mocking
{
    /// <summary>
    /// Turns an arbitrary seed string into stable pseudo-random numbers, so a mock supplier
    /// adapter returns the same offer for the same (manufacturer, MPN) every time it's asked,
    /// without a database round-trip. Uses FNV-1a because string.GetHashCode() is not guaranteed
    /// stable across processes/.NET versions.
    /// </summary>
    public static class DeterministicMock
    {
        public static long Hash(string seed)
        {
            unchecked
            {
                const long fnvOffset = unchecked((long)14695981039346656037);
                const long fnvPrime = 1099511628211;

                var hash = fnvOffset;
                foreach (var b in Encoding.UTF8.GetBytes(seed ?? string.Empty))
                {
                    hash ^= b;
                    hash *= fnvPrime;
                }
                return hash & long.MaxValue;
            }
        }

        public static double NextDouble(string seed, double min, double max)
        {
            var fraction = (Hash(seed) % 100000) / 100000.0;
            return min + fraction * (max - min);
        }

        public static int NextInt(string seed, int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(Hash(seed) % (max - min));
        }

        public static T Pick<T>(string seed, T[] options)
        {
            return options[(int)(Hash(seed) % options.Length)];
        }
    }
}
