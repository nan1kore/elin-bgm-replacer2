# NkBGMReplacer2.dll

## Overview

**NkBGMReplacer2** is a mod for *Elin* that provides a BGM replacement system similar to the one in *Elona*.  
By preparing an MP3 file with the same name as the `zoneid` set for each map, this mod will play your custom music instead of the default BGM.  

Additionally, you can prepare sequential files named `zoneid_x` (where x = 1-9) to play them in order, allowing multiple tracks per zone.

Unlike the previous version, this mod no longer mutes the game’s original BGM and plays replacement audio via NAudio. Instead, it now uses *Elin*’s own BGM system to directly handle playback.

Although this feature is planned to be officially implemented, it has not yet been released.  
(It will likely require all maps to be unlocked before implementation.)  
While CWL can achieve this, it is somewhat complicated. This mod prioritizes **simplicity**, allowing BGM replacement in an *Elona*-like manner.

### Why Not on Steam Workshop?

- Created as a personal hobby project  
- Maintenance is irregular and unpredictable  
- The author does not always play *Elin*  

This project is intended for people who understand the dependencies and can compile it themselves.

---

## Build Instructions

1. Download the source code.  
2. Open the `.csproj` file and resolve all dependencies.  
3. Compile it as a **Class Library** to generate `NkBGMReplacer2.dll`.  

---

## Usage

### Folder Structure

Place the DLL and related files in the following folder structure:

```text
Elin
 └── Package
        └── Mod_NkBGMReplacer2
				NkBGMReplacer2.dll
				package.xml
				BGM
				└── zoneid.mp3...
```


### Adding Custom BGM

1. Add your replacement MP3 files into the `BGM` folder.  
   - File names should match the `zoneid` (e.g., `dungeon_ruin.mp3`)  
   - Or use sequential files: `zoneid_1.mp3`, `zoneid_2.mp3`, …  

2. To find `zoneid` values quickly, check:  
   `Package/_Lang_Chinese/Lang/CN/Game/Zone.xlsx`

   - Column A contains the `zoneid`.  
   - Example: To replace the BGM for Ruins Nephia, prepare:  
     `dungeon_ruin.mp3` or `dungeon_ruin_1.mp3`, `dungeon_ruin_2.mp3`, etc.  

3. Special `zoneid` values included:  
   - `boss` → BGM for Nephia bosses  
   - `victory` → BGM for defeat after Nephia bosses  
   - `home` → BGM for your home base  

---

## Configuration

1. Launch *Elin* with the mod loaded.  
2. A configuration file will be generated at:  
   `BepInEx/config/com.nan1kore.elinbgmreplacer2.cfg`  
3. Open it with a text editor. The following options are available:  
   - **BgmDirectory** → Absolute path to the folder containing your replacement MP3 files  
     - Only **absolute paths** are supported  
     - Defaults to the same folder where `NkBGMReplacer2.dll` is located  
   - **Volume** → Playback volume for all MP3 files (applied uniformly)  

> Individual file volume adjustments are **not supported**.  
> If you need per-track adjustments, please preprocess files with an external audio tool.

---

## Notes

- Ensure all dependencies are resolved when compiling the DLL.  
- The mod does **not** require Steam Workshop. Users must compile and use it manually.  
- Sequential BGM playback (`zoneid_1.mp3`, `zoneid_2.mp3`, etc.) allows a more dynamic soundtrack for each map.

---

## Disclaimer
- This project is provided **as-is**, with no guarantees of support or maintenance.  
- **Do not contact the Elin developers** about this mod; it is unofficial and unrelated to them.  
- **Do not redistribute this project (or its builds) on Steam Workshop or any other platform.**  
- You may use, study, and compile it for personal use only.
