## About MVDX2
* (TODO)

## Special Thanks
* TKGP - Made SoulsFormats, which this application depends on greatly.
* [Katalash](https://github.com/katalash) - Much help with animation file understanding.
* [PredatorCZ](https://github.com/PredatorCZ) - Reverse engineered Spline-Compressed Animation entirely.
* [Horkrux](https://github.com/horkrux) - Reverse engineered the header and swizzling used on non-PC platform textures.
* StaydMcButtermuffin - Many hours of helping me write and debug the shaders + reversing some basic Dark Souls 3 shaders to aid in the process.

## Libraries Utilized
* [My custom fork of SoulsFormats](https://github.com/Meowmaritus/SoulsFormats)
* [Newtonsoft Json.NET](https://www.newtonsoft.com/json)
* A custom build of MonoGame Framework by Katalash fixing some of the shitty limitations
* A small portion of [HavokLib](https://github.com/PredatorCZ/HavokLib), specifically the spline-compressed animation decompressor, adapted for C#
* A small portion of [Horkrux's copy of my fork of Wulf's BND Rebuilder](https://github.com/horkrux/DeS-BNDBuild), specifically the headerization and deswizzling of PS4 and PS3 textures, adapted for C# and modified to load the texture directly into MonoGame instead of save to a file.