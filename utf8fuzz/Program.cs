using System;
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

            Driver driver = new Driver(allBytes);
            driver.RunTest();
        }
    }
}
