# SCS games' Clutch Fix

## About this program

This is basically a "mod maker". It fetches base and DLC package files for
both American Truck Simulator (ATS) or Euro Truck Simulator 2 (ETS2) and
generate a local mod with all engine files changed to improve the respective
game's clutch actuation performance when using a manual gearbox with clutch
controller.

## About the mod it makes

This mod makes little, no, or negative impact difference in the game if played
with anything that does not employ an analog clutch pedal, such as keyuboar +
mouse, xbox/ps2 controller, gamepad, steering wheels that employ only brake +
gas pedals, and may also misbehave if you use automatic/sequential gearbox
settings in the respective game.

## How it works

Basically it builds a mod changing engine settings to employ higher torques at
lower RPM intervals.

### How the program works

1. Searches for Steam Installation
2. From Steam installation, checks for each, ATS and ETS2 "installed" status
3. For each installed game, searches the corresponding library path the game
is installed at
4. Prompts whether the mod for each game is to be built/refreshed
5. Opens each game's executable to extract exact version information from
6. Parses base.scs, def.scs and dlc_*.scs files for truck engine info files
7. Extract files preserving path
8. Adjusts torque curve with stronger values for low RPMs
9. Adds custom torque information for engines that uses default curves
10. Saves all files to the folder:
`%USERPROFILE%\Documents\<game>\mod\ClutchImprovements`
11. Creates mod manifest and description files

### How the mod works

The mod simply employs a steeper engine torque curve in the low ends such
that 45% torque is attained very early (~310rpm) then slowly approaching
50% at its idle speed.

With this, it becomes possible to "hold" the truck uphill if the clutch is
partially released, like in real cars. It is also possible to actually
take off without depressing the accelerator pedal in flat surfaces, just
like it can be done in actual cars (provided you have good clutch control).

## Disclaimer

What's originally done here is actually the ability to dynamically parse
all games and DLCs to extract every engine data available, and the clutch
feature applied to ETS2 and all ATS engines.

### This program's

The code to actually decode SCS files comes from sk-zk/TruckLib.HashFs project,
and several parts, especially `CityHash` class was reworked to include only
the minimally necessary code for this program's ends.

### The mod itself

The mod is based in the ["Engine Idle torque 'Fix'" mod in Steam workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=932750331)
which only changes a limited number of engines in the game, but uses a more
comprehensive methodology to determine changed torque values.

The mod has a backlog of game incompatibility between releases, which prompted
for this automated program to process all engines and make it easier to build
new releases, with hopefully a reduced chance to need changes in the actual
mod building logic.

### SCS documentation

Among several modding.scsgames.com documentation files, the most relevant for
this project is [the engine definition documentation](https://modding.scssoft.com/wiki/Documentation/Engine/Units/accessory_engine_data) which, among other things,
documents the default torque curve for engines, which needs to be checked in
between upgrades to ensure the mod's modification over default values still
makes sense.

So, credits on the knowledge about torque curves and the solution for several
engines that didn't employ explicit torque curves are to SCS.

## How to use

In short, this mod can be downloaded from Steam Workshop, so it's just a matter
of subscribing to it in Steam. There's one for ATS and another for ETS2.

### Steam Workshop

Probably easiest way, but may get outdated in time if I am not able to actively
keep it up-to-date as SCS makes new releases.

- [American Truck Simulator Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2957334673)
- [Euro Truck Simulator Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=2957334673)

### Local Mod

This repository will keep releases and the respective mods will be attached as
release assets. Unfortunately they can't be automagically generated as not only
the game installation is required, but also in Windows OS's.

The mod can just be built on your side too.

To actually enable the local mod, it is needed to enable it in mod browser at
profile selection screen, just right opening the game.

#### Release assets

Just grab the file corresponding to the mod and game you like in the latest
release in this GitHub repository.

Link will be available as soon as the first release is made.

#### Run the tool

Grab the actual Windows console application tool from the releases and run it.
The mod will be dumped right in your game's `mod` directory (created if it
does not exist). The tool will ask, for each detected game, whether to process
files and make the local mod.

#### Build and run the tool

This was built using Visual Studio 2022 but should work with the `dotnet`
CLI if you have .NET 6 SDK installed. Once built you can run the tool just
like the one downloaded from Releases. Perhaps as short as a `dotnet run`.

### Multiplayer

This mod shouldn't affect multiplayer (Convoys, TruckersMP) in any way, so it
is marked as 'Optional' for whichever server considers optional mods actually
"optional".

## Limitations

This mod creation tool has its limitations, and for the known ones:

- Doesn't support walking thru mods to fetch engine info and place them
altogether. It means, no modded truck engine support; this mod won't apply
to modded trucks unless they use engines from game's base and DLCs.

- The tool won't fully refresh the mod once the game is running. The game locks
mod files when it loads the mod and only releases them on quit.

- The tool won't make a .zip (or .scs) file from the mod. The mod will be
fully exposed in the mod folder (but within its own directory, without messing
up things).

- Unfortunately, how it tries to attain a better-realistic clutch actuation
implies in a surreal engine torque ratio for lower RPM. Still, to me at least,
the overall experience in the game (using manual shifting) improved.

## Recommended settings

This mod is intended to work with a more "realistic" clutch actuation area.
This much is possible in the game, by setting the clutch axis' deadzone at
around, say, 10% of the slider, and the clutch range limit (the next slider)
to ~40%. The clutch pedal's "height" can be adjusted by increasing/decreasing
the deadzone; the higher the lower the clutch will be in the pedal.

## Feedback

The aim of this mod is to make the game more realistic. The way it works,
without modifications, makes it so taking off from a standstill unrealistically
difficult, even more if on a hill, loaded. There are some weird tricks, like
"shaking" the clutch to make a heavy truck start in an uphill, which is simply
not realistic.

This mod tries to make the clutch behave more like an actual car by giving it
some more power, such that it still stalls if the clutch is released abruptly,
yet, if held firm in the right bite position the truck could start moving
without the need to smash the gas pedal like it does in stock game settings