using System;
using System.Diagnostics;
using System.IO;
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
        private static IntPtr renderer;
        
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
                (StatusCode status, PngPixel[][] pixels, ImageHeaderChunk header) = Parse(File.OpenRead(args[0]));
                sw.Stop();
                
                if (status == StatusCode.None)
                {
                    Console.WriteLine($"Decoding and getting the image pixels took: {sw.Elapsed.TotalSeconds} seconds");
                    
                    sw.Restart();
                    
                    SDL_Init(SDL_INIT_VIDEO | SDL_INIT_EVENTS);
                    window = SDL_CreateWindow("PngViewer", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, (int)header.Width,
                        (int)header.Height, SDL_WindowFlags.SDL_WINDOW_OPENGL);
                    
                    renderer = SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

                    SDL_ShowWindow(window);

                    SDL_RenderClear(renderer);

                    for (short i = 0; i < pixels.Length; i++)
                    {
                        for (short j = 0; j < pixels[i].Length; j++)
                        {
                            PngPixel pixel = pixels[i][j];
                            pixelRGBA(renderer, j, i, (byte)pixel.Red, (byte)pixel.Green, (byte)pixel.Blue, (byte)pixel.Alpha);
                        }
                    }
                    
                    SDL_RenderPresent(renderer);
                    
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
    }
}