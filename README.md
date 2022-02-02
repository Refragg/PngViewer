# PngViewer

## Usage:
./PngViewer "filename"

## What it is
This is a very simple image viewer that supports PNG images.  
It uses [zlib](http://www.zlib.net/) to decompress the data contained in the image, [SDL2](https://www.libsdl.org/index.php) and [SDL2_gfx](https://www.ferzkopp.net/wordpress/2016/01/02/sdl_gfx-sdl2_gfx/) to display the image on the screen.  
  
The main goal of this was to explore how to parse the data inside a PNG image so the data processing part is self made using the [PNG Specification](http://www.libpng.org/pub/png/spec/1.2/PNG-Contents.html).  
This implementation doesn't include everything that the PNG format supports, but it is a start and works for a bunch of image types

## How to build it:
First, you want to build the PngParser project, go to the PngParser directory in a terminal.  
  
Since it is doing compile time OS checking to get the correct zlib library name, you want to specify your [target RID](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog) in the build command like this:
`dotnet build -r linux-x64` or for windows: `dotnet build -r win-x64`

If you get the warning CS0626 then the RID is probably not supported by the platform checking code, you can try removing that code and hardcoding your library name (in PngParser/PngParser.cs at the top of the PngParser class)

After building the PngParser, move to the PngViewer directory and edit the PngViewer.csproj to add a reference to the produced library file like this

```
    <ItemGroup>
      <Reference Include="PngParser, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null">
        <HintPath>..\PngParser\bin\[...]\PngParser.dll</HintPath>
      </Reference>
    </ItemGroup>
```

You can then simply build the PngViewer using this command:

`dotnet build`

The resulting executable will be in bin/(Debug or Release)/net5.0/  
Note: If you are on Windows, you might need to get the zlib.dll, SDL2.dll and SDL2_gfx.dll in the executable directory, You can find the 64 bits versions of these in the Windows x64 libs folder, simply copy them to the folder containing the executable  
  
You can now refer to the usage section of this Readme
