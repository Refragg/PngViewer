using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using PngParser;
using static PngParser.PngParser;
using static SDL2.SDL;
using static SDL2.SDL_gfx;

namespace PngViewer
{
    class Program
    {
        private static IntPtr window;
        
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No file provided");
                Environment.Exit(1);
            }
            
            if (File.Exists(args[0]))
            {
                Stopwatch sw = Stopwatch.StartNew();

                StatusCode status;
                PngPixel[][] pixels;
                ImageHeaderChunk header;
                
                using (FileStream fileStream = File.OpenRead(args[0]))
                    (status, pixels, header) = Parse(fileStream);
                
                sw.Stop();
                
                if (status == StatusCode.None)
                {
                    Console.WriteLine($"Decoding and getting the image pixels took: {sw.Elapsed.TotalSeconds} seconds");
                    
                    sw.Restart();
                    
                    SDL_Init(SDL_INIT_VIDEO | SDL_INIT_EVENTS);
                    window = SDL_CreateWindow("PngViewer", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, (int)header.Width,
                        (int)header.Height, SDL_WindowFlags.SDL_WINDOW_OPENGL);
                    
                    IntPtr windowSurfacePointer = SDL_GetWindowSurface(window);

                    SDL_ShowWindow(window);

                    WritePixelsToSurface(pixels, windowSurfacePointer, header);

                    SDL_UpdateWindowSurface(window);
                    
                    sw.Stop();
                    
                    Console.WriteLine($"Displaying the image to the screen took: {sw.Elapsed.TotalSeconds} seconds");
                    
                    SDL_Event sdlEvent;
                    
                    while (true)
                    {
                        if (SDL_PollEvent(out sdlEvent) == 1)
                        {
                            switch (sdlEvent.type)
                            {
                                case SDL_EventType.SDL_QUIT:
                                    goto end;
                            }
                        }
                        
                        Thread.Sleep(1);
                    }
                    
                    end:
                    return;
                }
                else
                    Console.WriteLine("Failed: {0}", status.ToString());
            }
            else
            {
                Console.WriteLine("Incorrect file path");
                Environment.Exit(1);
            }
        }

        private static unsafe void WritePixelsToSurface(in PngPixel[][] pixels, IntPtr surface, ImageHeaderChunk header)
        {
            SDL_Surface windowSurface = Marshal.PtrToStructure<SDL_Surface>(surface);

            uint pixelFormat = Marshal.PtrToStructure<SDL_PixelFormat>(windowSurface.format).format;

            if (pixelFormat != SDL_PIXELFORMAT_RGB888)
                throw new NotImplementedException($"Writing pixels to a surface with a pixel format of {SDL_GetPixelFormatName(pixelFormat)} is not implemented");
            
            byte* pixelsPointer = (byte*)windowSurface.pixels.ToPointer();

            int lineByteLength = windowSurface.pitch;

            for (uint y = 0; y < windowSurface.h; y++)
            {
                for (uint x = 0; x < windowSurface.w; x++)
                {
                    PngPixel pixel = pixels[y][x];

                    ((uint*)pixelsPointer)[x] = SDL_MapRGBA(windowSurface.format, (byte)pixel.Red, (byte)pixel.Green, (byte)pixel.Blue, (byte)pixel.Alpha);
                }
                
                pixelsPointer += lineByteLength;
            }

            Marshal.StructureToPtr(windowSurface, surface, true);
        }
    }
}