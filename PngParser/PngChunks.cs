using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static PngParser.PngParser;
using static PngParser.Helpers;

namespace PngParser
{
    public static class PngChunks
    {
        private static class ChunkTypeNames
        {
            public const string ImageHeader = "IHDR";
            public const string Palette = "PLTE";
            public const string ImageData = "IDAT";
            public const string ImageEnd = "IEND";
            
            public const string Chromaticities = "cHRM";
            public const string Gamma = "gAMA";
            public const string IccProfile = "iCCP";
            public const string SignificantBits = "sBIT";
            public const string StandardRgb = "sRGB";
            public const string Background = "bKGD";
            public const string PaletteHistogram = "hIST";
            public const string Transparency = "tRNS";
            public const string PhysicalPixelDimensions = "pHYs";
            public const string SuggestedPalette = "sPLT";
            public const string LastModifiedTime = "tIME";
            public const string InternationalTextualData = "iTXt";
            public const string TextualData = "tEXt";
            public const string CompressedTextualData = "zTXt";
        }

        public static (StatusCode, PngChunk chunk) ReadNextChunk(Stream byteStream)
        {
            byte[] chunkLengthBytes = new byte[4];
            if (byteStream.Read(chunkLengthBytes, 0, chunkLengthBytes.Length) != chunkLengthBytes.Length)
                return (StatusCode.BadFile, null);

            uint chunkLength = BitConverter.ToUInt32(ReverseBytes(chunkLengthBytes));
            
            byte[] chunkTypeBytes = new byte[4];
            if (byteStream.Read(chunkTypeBytes, 0, chunkTypeBytes.Length) != chunkTypeBytes.Length)
                return (StatusCode.BadFile, null);

            string chunkType = Encoding.ASCII.GetString(chunkTypeBytes);

            byte[] chunkData = new byte[chunkLength];
            if (byteStream.Read(chunkData, 0, chunkData.Length) != chunkData.Length)
                return (StatusCode.BadFile, null);

            PngChunk chunk = null;

            //Console.WriteLine(chunkType);
            
            switch (chunkType)
            {
                case ChunkTypeNames.ImageHeader:
                    chunk = ImageHeaderChunk.ReadData(chunkData);
                    break;
                case ChunkTypeNames.ImageEnd:
                    chunk = ImageEndChunk.ReadData(chunkData);
                    break;
                case ChunkTypeNames.ImageData:
                    chunk = ImageDataChunk.ReadData(chunkData);
                    break;
                default:
                    break;
            }
            
            byte[] chunkCrc = new byte[4];
            if (byteStream.Read(chunkCrc, 0, chunkCrc.Length) != chunkCrc.Length)
                return (StatusCode.BadFile, null);

            if (chunk == null)
                return (StatusCode.UnrecognizedPngChunk, null); 
            
            return (StatusCode.None, chunk);
        }
    }

    public abstract class PngChunk
    {
        public uint DataLength;
    }
    
    public class ImageHeaderChunk : PngChunk
    {
        private const int ImageHeaderLength = 13;
        
        public uint Width;
        public uint Height;
        
        public byte BitDepth;
        public ColorType Color;
        public byte CompressionMethod;
        public byte FilterMethod;
        public byte InterlaceMethod;

        [Flags]
        public enum ColorType
        {
            GreyScale = 0,
            PaletteUsed = 1,
            ColorUsed = 2,
            AlphaChannelUsed = 4
        }

        private ImageHeaderChunk()
        {
        }

        internal static ImageHeaderChunk ReadData(byte[] chunkData)
        {
            if (chunkData.Length != ImageHeaderLength)
                throw new ArgumentException("Image Header is invalid", nameof(chunkData));

            ImageHeaderChunk chunk = new ImageHeaderChunk()
            {
                Width = BitConverter.ToUInt32(ReverseBytes(chunkData[..4])),
                Height = BitConverter.ToUInt32(ReverseBytes(chunkData[4..8])),

                BitDepth = chunkData[8],
                Color = (ColorType)chunkData[9],
                CompressionMethod = chunkData[10],
                FilterMethod = chunkData[11],
                InterlaceMethod = chunkData[12],
            };

            return chunk;
        }
        
        public override string ToString()
        {
            return $"----ImageHeaderChunk:----\n\tWidth: {Width}, Height: {Height}, BitDepth: {BitDepth}, ColorType: {Color.ToString()}, CompressionMethod: {CompressionMethod}, FilterMethod: {FilterMethod}, InterlaceMethod: {InterlaceMethod}";
        }
    }

    public class ImageEndChunk : PngChunk
    {
        private ImageEndChunk()
        {
        }
        
        internal static ImageEndChunk ReadData(byte[] chunkData)
        {
            if (chunkData.Length != 0)
                throw new ArgumentException("Image End is invalid", nameof(chunkData));

            ImageEndChunk chunk = new ImageEndChunk();

            return chunk;
        }
        
        public override string ToString()
        {
            return $"----ImageEndChunk----";
        }
    }

    public class ImageDataChunk : PngChunk
    {
        public byte[] Data;
        
        private ImageDataChunk()
        {
        }
        
        internal static ImageDataChunk ReadData(byte[] chunkData)
        {
            if (chunkData.Length == 0)
                throw new ArgumentException("Image Data is invalid", nameof(chunkData));

            ImageDataChunk chunk = new ImageDataChunk();

            chunk.Data = chunkData;

            return chunk;
        }
        
        public override string ToString()
        {
            return $"----ImageDataChunk----";
        }
    }
}