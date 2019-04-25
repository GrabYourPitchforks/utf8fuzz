using System;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Text.Unicode;

namespace utf8fuzz
{
    public class Driver
    {
        private readonly BoundedMemory<byte> _data;

        public Driver(BoundedMemory<byte> data)
        {
            _data = data;
        }

        public void RunTest()
        {
            Console.WriteLine("-- BEGIN TEST --");

            int encodingCharCount = Encoding.UTF8.GetCharCount(_data.Span);
            Console.WriteLine($"Encoding.UTF8.GetCharCount returned {encodingCharCount}.");

            {
                ReadOnlySpan<byte> input = _data.Span;
                int runeIterCharCount = 0;
                while (!input.IsEmpty)
                {
                    Rune.DecodeFromUtf8(input, out Rune thisRune, out int bytesConsumed);
                    runeIterCharCount += thisRune.Utf16SequenceLength; // ok if U+FFFD replacement
                    input = input.Slice(bytesConsumed);
                }

                Console.WriteLine($"Rune iteration said there were {runeIterCharCount} UTF-16 chars.");

                if (encodingCharCount != runeIterCharCount)
                {
                    throw new Exception("Rune iteration char count mismatch!!");
                }
            }

            char[] chars = new char[encodingCharCount];
            int charsWritten = Encoding.UTF8.GetChars(_data.Span, chars);
            Console.WriteLine($"Encoding.UTF8.GetChars returned {charsWritten} chars written.");

            if (encodingCharCount != charsWritten)
            {
                throw new Exception("GetChars return value mismatch!!");
            }

            {
                ReadOnlySpan<byte> inputUtf8 = _data.Span;
                ReadOnlySpan<char> inputUtf16 = chars;

                while (!inputUtf8.IsEmpty && !inputUtf16.IsEmpty)
                {
                    Rune.DecodeFromUtf8(inputUtf8, out Rune inputUtf8Rune, out int bytesConsumed);
                    Rune.DecodeFromUtf16(inputUtf16, out Rune inputUtf16Rune, out int charsConsumed);

                    if (inputUtf8Rune != inputUtf16Rune)
                    {
                        throw new Exception("Enumerating runes mismatch!!");
                    }

                    inputUtf8 = inputUtf8.Slice(bytesConsumed);
                    inputUtf16 = inputUtf16.Slice(charsConsumed);
                }

                if (inputUtf8.Length != inputUtf16.Length)
                {
                    throw new Exception("Rune enumeration returned mismatched lengths!");
                }
            }

            Console.WriteLine("Running ToUtf16 with replace=true and exact size buffer.");

            {
                char[] chars2 = new char[chars.Length];
                OperationStatus opStatus = Utf8.ToUtf16(_data.Span, chars2, out int bytesReadJustNow, out int charsWrittenJustNow, replaceInvalidSequences: true, isFinalBlock: true);

                if (opStatus != OperationStatus.Done)
                {
                    throw new Exception("Utf8.ToUtf16 returned wrong OperationStatus!!");
                }

                if (bytesReadJustNow != _data.Memory.Length)
                {
                    throw new Exception("Utf8.ToUtf16 didn't read entire input!!");
                }

                if (charsWrittenJustNow != chars2.Length)
                {
                    throw new Exception("Utf8.ToUtf16 didn't fill entire response buffer!!");
                }

                if (!chars.SequenceEqual(chars2))
                {
                    throw new Exception("Utf8.ToUtf16 returned different data than Encoding.UTF8.GetChars!!");
                }
            }

            Console.WriteLine("Running ToUtf16 with replace=true and extra large buffer.");

            {
                char[] chars2 = new char[chars.Length + 1024];
                OperationStatus opStatus = Utf8.ToUtf16(_data.Span, chars2, out int bytesReadJustNow, out int charsWrittenJustNow, replaceInvalidSequences: true, isFinalBlock: true);

                if (opStatus != OperationStatus.Done)
                {
                    throw new Exception("Utf8.ToUtf16 returned wrong OperationStatus!!");
                }

                if (bytesReadJustNow != _data.Memory.Length)
                {
                    throw new Exception("Utf8.ToUtf16 didn't read entire input!!");
                }

                if (charsWrittenJustNow != chars.Length)
                {
                    throw new Exception("Utf8.ToUtf16 didn't fill entire response buffer!!");
                }

                if (!chars2.AsSpan(0, charsWrittenJustNow).SequenceEqual(chars))
                {
                    throw new Exception("Utf8.ToUtf16 returned different data than Encoding.UTF8.GetChars!!");
                }
            }

            Console.WriteLine("Running ToUtf16 with replace=false and extra large buffer.");

            {
                ReadOnlySpan<byte> input = _data.Span;
                Span<char> output = new char[chars.Length + 1024];

                while (!input.IsEmpty)
                {
                    OperationStatus opStatus = Utf8.ToUtf16(input, output, out int bytesReadJustNow, out int charsWrittenJustNow, replaceInvalidSequences: false, isFinalBlock: true);

                    ReadOnlySpan<byte> dataReadJustNow = input.Slice(0, bytesReadJustNow);
                    ReadOnlySpan<char> dataWrittenJustNow = output.Slice(0, charsWrittenJustNow);

                    while (!dataReadJustNow.IsEmpty && !dataWrittenJustNow.IsEmpty)
                    {
                        OperationStatus utf8Status = Rune.DecodeFromUtf8(dataReadJustNow, out Rune inputUtf8Rune, out int bytesConsumed);
                        OperationStatus utf16Status = Rune.DecodeFromUtf16(dataWrittenJustNow, out Rune inputUtf16Rune, out int charsConsumed);

                        if (utf8Status != OperationStatus.Done)
                        {
                            throw new Exception("DecodeFromUtf8 returned unexpected value!!");
                        }

                        if (utf16Status != OperationStatus.Done)
                        {
                            throw new Exception("DecodeFromUtf16 returned unexpected value!!");
                        }

                        if (inputUtf8Rune != inputUtf16Rune)
                        {
                            throw new Exception("Enumerating runes mismatch!!");
                        }

                        dataReadJustNow = dataReadJustNow.Slice(bytesConsumed);
                        dataWrittenJustNow = dataWrittenJustNow.Slice(charsConsumed);
                    }

                    if (dataReadJustNow.Length != dataWrittenJustNow.Length)
                    {
                        throw new Exception("Unexpected length mismatch!!");
                    }

                    input = input.Slice(bytesReadJustNow);

                    if (opStatus != OperationStatus.Done)
                    {
                        // Skip over invalid data

                        Rune.DecodeFromUtf8(input, out _, out int bytesToSkip);
                        input = input.Slice(bytesToSkip);
                    }
                }
            }

            Console.WriteLine("Trying custom decoder replacement.");

            {
                // use a custom replacement string
                Encoding encoding = Encoding.GetEncoding("utf-8", EncoderFallback.ExceptionFallback, new DecoderReplacementFallback("{BAD}"));

                string decoded = encoding.GetString(_data.Span);

                ReadOnlySpan<byte> input = _data.Span;
                char[] decoded2 = new char[decoded.Length];
                StringBuilder builder = new StringBuilder();

                while (!input.IsEmpty)
                {
                    OperationStatus opStatus = Utf8.ToUtf16(input, decoded2, out int bytesReadJustNow, out int charsWrittenJustNow, replaceInvalidSequences: false, isFinalBlock: true);
                    builder.Append(decoded2, 0, charsWrittenJustNow);

                    input = input.Slice(bytesReadJustNow);

                    if (opStatus != OperationStatus.Done)
                    {
                        // Skip over invalid data

                        Rune.DecodeFromUtf8(input, out _, out int bytesToSkip);
                        input = input.Slice(bytesToSkip);

                        builder.Append("{BAD}");
                    }
                }

                if (new string(decoded) != builder.ToString())
                {
                    throw new Exception("Custom decoder replacement failed!!");
                }
            }

            Console.WriteLine("-- END TEST - SUCCESS --");
        }
    }
}
