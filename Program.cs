using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pcg;
using SimpleBase;

namespace Suicune
{
    /// <summary>
    /// Brute force the key used to encrypt the flag.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Pre-calculated step counts for
        /// reversing reverse sorted postfixes
        /// </summary>
        static readonly ulong[] STEP_COUNTS = new ulong[] {
            1,
            1,
            2,
            6,
            24,
            120,
            720,
            5040,
            40320,
            362880,
            3628800,
            39916800,
            479001600UL,
            6227020800UL,
            87178291200UL,
            1307674368000UL,
            20922789888000UL,
            355687428096000UL,
            6402373705728000UL,
            121645100408832000UL,
            2432902008176640000UL
        };

        /// <summary>
        /// Implements one step of the
        /// original sorting algorithm.
        /// </summary>
        /// <param name="input">Input</param>
        static bool SortStep(byte[] input)
        {
            // Start with the last two indices
            int firstSwapIndex = input.Length - 2;
            int secondSwapIndex = input.Length - 1;

            // Pick first index
            while (input[firstSwapIndex] > input[firstSwapIndex + 1])
            {
                if (firstSwapIndex == 0)
                {
                    // Sorted
                    return true;
                }
                else
                {
                    --firstSwapIndex;
                }
            }

            // Pick second index
            while (input[secondSwapIndex] < input[firstSwapIndex])
            {
                --secondSwapIndex;
            }

            // Swap
            var tmp = input[firstSwapIndex];
            input[firstSwapIndex] = input[secondSwapIndex];
            input[secondSwapIndex] = tmp;

            // Reverese
            Array.Reverse(input, firstSwapIndex + 1, input.Length - firstSwapIndex - 1);

            // Not sorted
            return false;
        }

        /// <summary>
        /// Accelerated version of the original algorithm
        /// that uses step estimation to determine where
        /// the sorting should stop.
        /// </summary>
        /// <param name="input">Input</param>
        /// <param name="limit">Limit</param>
        static void FastShittySort(byte[] input, ulong limit)
        {
            while (limit > 0)
            {
                // Count reverse sorted bytes
                // at the end of the input data
                int index = input.Length - 2;
                while (index > 0 && input[index] < input[index + 1])
                {
                    index--;
                }
                int length = input.Length - index - 1;

                // If we have such a section, just reverse it and decrement
                // the limit by the pre-calculated step count
                if (length > 2 && length < STEP_COUNTS.Length && limit >= STEP_COUNTS[length] - 1)
                {
                    // Reverse 
                    Array.Reverse(input, index + 1, length);

                    // Decrement limit
                    limit -= STEP_COUNTS[length] - 1;
                }
                else
                {
                    // Perform one standard sorting step
                    if (SortStep(input))
                    {
                        return;
                    }

                    // Decrement limit
                    --limit;
                }
            }
        }

        /// <summary>
        /// Generate the XOR key for a specific PRNG seed.
        /// </summary>
        /// <param name="seed">Seed</param>
        /// <param name="outputLength">Output length</param>
        /// <returns>XOR key</returns>
        static byte[] GenerateXorKey(ulong seed, int outputLength)
        {
            var buffer = new byte[256];
            var key = new byte[outputLength];
            var component = new byte[outputLength];

            // Initialize random generator
            var prng = new Pcg32(seed, 0);

            // Each key is generated over 16 iterations
            for (int i = 0; i < 16; ++i)
            {
                // Generate base array 
                for (int j = 0; j < buffer.Length; ++j)
                {
                    buffer[j] = (byte)j;
                }

                // Shuffle array
                for (int j = buffer.Length - 1; j >= 1; --j)
                {
                    var index = prng.GenerateNext((uint)j + 1);
                    var old = buffer[index];
                    buffer[index] = buffer[j];
                    buffer[j] = old;
                }

                // We only need the first N elements
                Array.Copy(buffer, 0, component, 0, component.Length);

                // Generate limit
                var part1 = prng.GenerateNext();
                var part2 = prng.GenerateNext();
                ulong limit = ((ulong)part2 << 32) | part1;

                // Sort
                FastShittySort(component, limit);

                // XOR with current candidate
                for (int j = 0; j < key.Length; ++j)
                {
                    key[j] ^= component[j];
                }

                // Reverse candidate
                Array.Reverse(key);
            }

            // Return key
            return key;
        }

        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">Arguments</param>
        static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                // Process input
                var prefixBytes = Encoding.ASCII.GetBytes(args[0]);
                var knownOutputBytes = Base16.Decode(args[1]).ToArray();

                // Determine XOR key to look for
                var prefixKey = prefixBytes.Select((b, i) => (byte)(b ^ knownOutputBytes[i])).ToArray();

                // Iterate over all possible keys              
                Parallel.For(0, UInt16.MaxValue + 1, (seed, state) =>
                {
                    // Generate key
                    var xorKey = GenerateXorKey((ulong)seed, knownOutputBytes.Length);

                    // Check if we got it right
                    if (xorKey.Take(prefixKey.Length).SequenceEqual(prefixKey))
                    {
                        // Print seed
                        Console.WriteLine($"Seed: {seed:X8}");

                        // Decode flag
                        var decodedBytes = knownOutputBytes.Select((b, i) => (byte)(b ^ xorKey[i])).ToArray();

                        // Print flag
                        var decodedString = Encoding.ASCII.GetString(decodedBytes);
                        Console.WriteLine($"Flag: {decodedString}");

                        // Success
                        state.Stop();
                    }
                });
            }
            else
            {
                Console.WriteLine("Usage: Suicune <known prefix> <output>");
            }
        }
    }
}
