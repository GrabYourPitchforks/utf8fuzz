using System;
using System.Buffers;
using System.IO;

namespace utf8fuzz
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Input file: {args[0]}");

            byte[] allBytes = File.ReadAllBytes(args[0]);
            Console.WriteLine($"({allBytes.Length} bytes)");

            // Is BOM present?

            if (allBytes.Length >= 3 && allBytes[0] == 0xEF && allBytes[1] == 0xBB && allBytes[2] == 0xBF)
            {
                Console.WriteLine("BOM present - removing.");
                allBytes = allBytes[3..];
                Console.WriteLine($"({allBytes.Length} bytes remain)");
            }

            using BoundedMemory<byte> boundedMemory = BoundedMemory.AllocateFromExistingData(allBytes);
            boundedMemory.MakeReadonly();

            Driver driver = new Driver(boundedMemory);
            driver.RunTest();
        }
    }
}
