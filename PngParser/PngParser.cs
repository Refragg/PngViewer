using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static PngParser.Helpers;

namespace PngParser
{
    public struct PngPixel
    {
        public ushort GrayScale;
        public ushort Red;
        public ushort Green;
        public ushort Blue;
        public ushort Alpha;
        
        public PngPixel(ushort grayScale, ushort red, ushort green, ushort blue, ushort alpha)
        {
            GrayScale = grayScale;
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }
        
        public PngPixel(ushort red, ushort green, ushort blue)
        {
            GrayScale = 0;
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = 255;
        }
        
        public PngPixel(ushort red, ushort green, ushort blue, ushort alpha)
        {
            GrayScale = 0;
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }
    }
    
    public class PngParser
    {
        #if Linux
        [DllImport("z", EntryPoint = "uncompress")]
        #elif Windows
        [DllImport("zlib.dll", EntryPoint = "uncompress")]
        #endif
        private static extern int Uncompress(IntPtr dest, ref ulong destSize, IntPtr src, ref ulong srcSize);

        public enum StatusCode
        {
            None,
            EndOfFile,
            BadHeader,
            BadFile,
            UnrecognizedPngChunk
        }

        private static readonly byte[] HeaderBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };
        
        public static (StatusCode statusCode, PngPixel[][] pixelsIfSuccess, ImageHeaderChunk header) Parse(Stream byteStream)
        {
            if (!(byteStream.CanRead || byteStream.CanSeek))
                throw new ArgumentException("The provided stream needs to be able to read and seek",
                    nameof(byteStream));

            byteStream.Seek(0, SeekOrigin.Begin);

            byte[] header = new byte[8];
            if (byteStream.Read(header, 0, header.Length) != header.Length)
                return (StatusCode.BadHeader, null, null);

            if (!CompareBytes(header, HeaderBytes))
                return (StatusCode.BadHeader, null, null);

            StatusCode statusCode = StatusCode.None;
            PngChunk chunk;

            ImageHeaderChunk imageHeaderChunk = null;

            List<byte> bytes = new List<byte>();
            
            while (statusCode == StatusCode.None || statusCode == StatusCode.UnrecognizedPngChunk)
            {
                (statusCode, chunk) = PngChunks.ReadNextChunk(byteStream);

                if (statusCode == StatusCode.None)
                {
                    if (chunk is ImageEndChunk)
                        break;
                    if (chunk is ImageHeaderChunk headerChunk)
                    {
                        imageHeaderChunk = headerChunk;
                        //Console.WriteLine(headerChunk.ToString());
                    }
                    if (chunk is ImageDataChunk dataChunk)
                        bytes.AddRange(dataChunk.Data);
                }
            }

            if (statusCode == StatusCode.None)
            {
                byte[] compressedBytes = bytes.ToArray();

                (bool worked, byte[] uncompressedBytes) = Uncompress(compressedBytes);

                if (!worked) 
                    return (StatusCode.BadFile, null, imageHeaderChunk);
                
                (bool success, PngPixel[][] pixels) = ProcessImageData(uncompressedBytes, imageHeaderChunk);

                if (!success)
                {
                    Console.WriteLine("Bad Image Data");
                    return (StatusCode.BadFile, null, imageHeaderChunk);
                }

                return (StatusCode.None, pixels, imageHeaderChunk);
            }

            return (statusCode, null, imageHeaderChunk);
        }
        
        private static (bool success, PngPixel[][] pixels) ProcessImageData(byte[] imageData, ImageHeaderChunk headerChunk)
        {
            int scanLineLength = GetScanLineLength(headerChunk);

            if (scanLineLength == -1)
                return (false, null);
            
            PngPixel[][] pixels = new PngPixel[headerChunk.Height][];

            Span<byte> previousUnmodifiedScanLine = new byte[scanLineLength];

            Span<byte> imageDataSpan = imageData;

            for (int i = 0; i < headerChunk.Height; i++)
            {
                Span<byte> scanLine = imageDataSpan.Slice(0, scanLineLength);

                if (imageDataSpan[0] != 0)
                    ApplyFilter(scanLine, previousUnmodifiedScanLine, headerChunk, (Filters)imageDataSpan[0]);
                
                pixels[i] = ProcessScanLine(scanLine.Slice(1), headerChunk);

                previousUnmodifiedScanLine = scanLine;
                imageDataSpan = imageDataSpan.Slice(scanLineLength);
            }

            return (true, pixels);
        }

        private enum Filters : byte
        {
            None = 0,
            Sub = 1,
            Up = 2,
            Average = 3,
            Paeth = 4
        }
        
        private static void ApplyFilter(Span<byte> scanLine, Span<byte> previousScanLine, ImageHeaderChunk headerChunk, Filters filter)
        {
            int bpp = (int)(scanLine.Length / headerChunk.Width);
            
            scanLine[0] = 0;
            previousScanLine[0] = 0;
            
            if (filter == Filters.Sub)
            {
                for (int i = 0; i < scanLine.Length; i++)
                {
                    int minusBpp = Math.Max(0, i - bpp);
                    
                    scanLine[i] = (byte)((scanLine[i] + scanLine[minusBpp]) & 0xff);
                }
            }
            else if (filter == Filters.Up)
            {
                for (int i = 0; i < scanLine.Length; i++)
                {
                    scanLine[i] = (byte)((scanLine[i] + previousScanLine[i]) & 0xff);
                }
            }
            else if (filter == Filters.Average)
            {
                for (int i = 0; i < bpp; i++)
                {
                    scanLine[i] = (byte)((scanLine[i] + previousScanLine[i] / 2) & 0xff);
                }

                for (int i = 0; i < scanLine.Length - bpp; i++)
                {
                    int minusBpp = Math.Max(0, i - bpp);
                    
                    scanLine[i] = (byte)((scanLine[i] + (previousScanLine[i] + scanLine[minusBpp]) / 2) & 0xff);
                }
            }
            else if (filter == Filters.Paeth)
            {
                var paethPredictor = new Func<byte, byte, byte, byte>((left, above, upperLeft) =>
                {
                    short p = (short)(left + above - upperLeft);

                    ushort pLeft = (ushort)Math.Abs(p - left);
                    ushort pAbove = (ushort)Math.Abs(p - above);
                    ushort pUpperLeft = (ushort)Math.Abs(p - upperLeft);

                    if (pLeft <= pAbove && pLeft <= pUpperLeft)
                        return left;
                    else if (pAbove <= pUpperLeft)
                        return above;
                    else
                        return upperLeft;
                });

                for (int i = 0; i < scanLine.Length; i++)
                {
                    int minusBpp = Math.Max(0, i - bpp);

                    scanLine[i] = (byte)(scanLine[i] + (paethPredictor(scanLine[minusBpp], previousScanLine[i], previousScanLine[minusBpp]) & 0xff));
                }
            }
        }

        private static int GetScanLineLength(ImageHeaderChunk headerChunk)
        {
            int byteDepth = headerChunk.BitDepth / 8;

            switch (headerChunk.Color)
            {
                case ImageHeaderChunk.ColorType.GreyScale:
                case ImageHeaderChunk.ColorType.PaletteUsed | ImageHeaderChunk.ColorType.ColorUsed:
                    throw new NotImplementedException("Color type 0 and 3 of the scan lines are not implemented");
                case ImageHeaderChunk.ColorType.ColorUsed:
                    return (int)(3 * byteDepth * headerChunk.Width + 1);
                case ImageHeaderChunk.ColorType.AlphaChannelUsed:
                    return (int)(2 * byteDepth * headerChunk.Width + 1);
                case ImageHeaderChunk.ColorType.AlphaChannelUsed | ImageHeaderChunk.ColorType.ColorUsed:
                    return (int)(4 * byteDepth * headerChunk.Width + 1);
                default:
                    return -1;
            }
        }

        private static readonly string BitDepthWarning =
            "WARNING: {0} bit depth has not been tested and might break (only 8 bit has been tested)";

        private static PngPixel[] ProcessScanLine(Span<byte> scanLine, ImageHeaderChunk headerChunk)
        {
            PngPixel[] pixels = new PngPixel[headerChunk.Width];
            
            int byteDepth = headerChunk.BitDepth / 8;

            if (headerChunk.Color == ImageHeaderChunk.ColorType.ColorUsed)
            {
                if (headerChunk.BitDepth == 8)
                {
                    Span<byte> tempArray = scanLine;
                    
                    for (int i = 0; i < headerChunk.Width; i++)
                    {
                        pixels[i] = new PngPixel(tempArray[0], tempArray[1],  tempArray[2]);

                        tempArray = tempArray.Slice(3);
                    }
                }
                else
                {
                    Console.WriteLine(BitDepthWarning, headerChunk.BitDepth);
                    
                    Span<byte> tempArray = scanLine;
                    
                    for (int i = 0; i < headerChunk.Width; i++)
                    {
                        ushort red = BitConverter.ToUInt16(ReverseBytes(tempArray.Slice(0, 2).ToArray()));
                        ushort green = BitConverter.ToUInt16(ReverseBytes(tempArray.Slice(2, 4).ToArray()));
                        ushort blue = BitConverter.ToUInt16(ReverseBytes(tempArray.Slice(4, 6).ToArray()));
                        pixels[i] = new PngPixel(red, green, blue);
                        
                        tempArray = tempArray.Slice(6);
                    }
                }
            }
            else if (headerChunk.Color == ImageHeaderChunk.ColorType.AlphaChannelUsed)
            {
                throw new NotImplementedException();
            }
            else
            {
                if (headerChunk.BitDepth == 8)
                {
                    Span<byte> tempArray = scanLine;
                    
                    for (int i = 0; i < headerChunk.Width; i++)
                    {
                        pixels[i] = new PngPixel(tempArray[0],  tempArray[1], tempArray[2], tempArray[3]);

                        tempArray = tempArray.Slice(4);
                    }
                }
                else
                {
                    Console.WriteLine(BitDepthWarning, headerChunk.BitDepth);
                    
                    Span<byte> tempArray = scanLine;
                    
                    for (int i = 0; i < headerChunk.Width; i++)
                    {
                        ushort red = BitConverter.ToUInt16(ReverseBytes(tempArray.Slice(0, 2).ToArray()));
                        ushort green = BitConverter.ToUInt16(ReverseBytes(tempArray.Slice(2, 4).ToArray()));
                        ushort blue = BitConverter.ToUInt16(ReverseBytes(tempArray.Slice(4, 6).ToArray()));
                        ushort alpha = BitConverter.ToUInt16(ReverseBytes(tempArray.Slice(6, 8).ToArray()));
                        pixels[i] = new PngPixel(red, green, blue, alpha);
                        
                        tempArray = tempArray.Slice(8);
                    }
                }
            }

            return pixels;
        }

        private static unsafe (bool worked, byte[] uncompressedBytes) Uncompress(byte[] source)
        {
            ulong sourceSize = (ulong) source.Length;
            ulong destinationSize = sourceSize * 2048;

            IntPtr destinationPointer = Marshal.AllocHGlobal((int)destinationSize);

            fixed (byte* pointer = source)
            {
                IntPtr sourcePointer = (IntPtr)pointer;

                int resultCode = Uncompress(destinationPointer, ref destinationSize, sourcePointer, ref destinationSize);

                if (resultCode != 0)
                {
                    Console.WriteLine("Uncompress error code: {0}", resultCode);
                    return (false, null);
                }
            }

            byte[] uncompressedBytes = new byte[destinationSize];
            
            Marshal.Copy(destinationPointer, uncompressedBytes, 0, (int)destinationSize);

            return (true, uncompressedBytes);
        }
    }
}