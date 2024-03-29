# Ghost Buster Mod - Challenge Ghost Replay
This is an `Ultimate Chicken Horse` `BepInEx` mod that enables ghost replays of challenge runs.



https://user-images.githubusercontent.com/1382274/178788019-aab8bc31-fa7a-4013-862c-f8063d854fd3.mp4



In Treehouse (with Challenge mode selected) or in Challenge level hotkeys:

| Key          |  Overrides                         |
| ---          |                                --- |
| G            | Toggle Ghosts on or off            |
| H            | Toggle Ghost Modes                 |
| Ctrl + L     | Load stored data from clipboard    |
| Ctrl + K     | Store ghost data in clipboard      |
| Ctrl + N     | Toggle text above Ghosts           |
| Ctrl + X     | Clear all GhostReplay data         |

(Keybindings can be changed in the config file `BepInEx\config\GhostBuster.cfg`.)

Ghost Modes:
 - (default) Fastest run
 - All runs (Max via MaxGhostNumber in config)
 - Last run
 - Stored Clipboard ghost
 
TODO:
 - GUI
 - Import/Export
 - Online sync
 - Fixup input replay
 - Input display
 - Timeline/Event display
 - Editor
 - Ghost Trail, show the path of a ghost trough the level
 
## Thunderstore installation
The mod is available via [thunderstore.io](https://thunderstore.io/c/ultimate-chicken-horse/) and can be installed using [r2modman](https://github.com/ebkr/r2modmanPlus/releases/latest).

## Manual installation
- Download [BepInEx Version 5](https://github.com/BepInEx/BepInEx/releases/latest) for your platform (windows64 or linux) (UCH is a x64 program)
- Download [the latest UCH-GhostBuster release (GhostBuster-x.x.x.x.zip)](https://github.com/batram/UCH-GhostBuster/releases) 
- Put all the contents inside the zip files into your `Ultimate Chicken Horse` folder found via `Steam -> Manage -> Browse Local Files`.
  (Just drag the Bepinex folder from the zip to your game folder.)
Run game! (Linux users need an additional step, follow instructions in BepInEx)

## Help
If you have questions, comments or suggestions join the [UCH Mods discord](https://discord.gg/GgzDQW6zbq)


## Build with dotnet
1. Download the source code of the mod (or use git):
      - https://github.com/batram/UCH-GhostBuster/archive/refs/heads/main.zip

2. Extract the folder at a location of your choice (the source code should not be in the `BepInEx` plugins folder)

3. Install dotnet (SDK x64):
      - https://dotnet.microsoft.com/en-us/download

4. Make sure you have BepInEx installed:
      - Download [BepInEx](https://github.com/BepInEx/BepInEx/releases) for your platform (UCH is a x64 program)
      - Put all the contents from the `BepInEx_x64` zip file into your `Ultimate Chicken Horse` folder found via `Steam -> Manage -> Browse Local Files`.

5. Click on the `build.bat` file in the source code folder `UCH-GhostBuster-main` you extracted 

## Config and Issues
1. UCH installation path
      - If Ultimate Chicken Horse is not installed at the default steam location, 
  the correct path to the installation needs to be set in `GhostBuster.csproj`.
      - You can edit the `GhostBuster.csproj` file with any Text editor (e.g. notepad, notepad++). 
      - Replace the file path between `<UCHfolder>` and `</UCHfolder>` with your correct Ultimate Chicken Horse game folder.

            <PropertyGroup>
              <UCHfolder>C:\Program Files (x86)\Steam\steamapps\common\Ultimate Chicken Horse\</UCHfolder>
            </PropertyGroup>
      
      - If the path is wrong you see the following errors during the build:

            ...
            warning MSB3245: Could not resolve this reference. Could not locate the assembly "Assembly-CSharp"
            warning MSB3245: Could not resolve this reference. Could not locate the assembly "UnityEngine"
            ...
            error CS0246: The type or namespace name 'UnityEngine' could not be found
            ...

2. Missing BepInEx
      - If the build errors only metion `BepInEx` and `0Harmony`, check that BepInEx is installed in your game folder
      - Example Errors (no other `MSB3245` warnings):

            warning MSB3245: Could not resolve this reference. Could not locate the assembly "BepInEx"
            warning MSB3245: Could not resolve this reference. Could not locate the assembly "0Harmony"
            ...
            error CS0246: The type or namespace name 'BepInEx' could not be found
            ...
              
      - correct folder structure:

            -> Ultimate Chicken Horse
                   -> BepInEx
                        -> core
                              -> 0Harmony.dll
                              -> ...
                   -> UltimateChickenHorse_Data
                   -> doorstop_config.ini
                   -> ...
                   -> UltimateChickenHorse.exe
                   -> ...
                   -> winhttp.dll


## Credits
- [Clever Endeavour Games](https://www.cleverendeavourgames.com/)
- [BepInEx](https://github.com/BepInEx/BepInEx) team
- [Harmony](https://github.com/pardeike/Harmony) by Andreas Pardeike
