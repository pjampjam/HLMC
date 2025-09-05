# Holy Lois Client Updater

A simple, portable Minecraft modpack updater for the Holy Lois Client.

## Features

- **Self-Contained:** Single EXE file - no installation required
- **Auto-Detection:** Automatically detects when placed in a Minecraft folder
- **Portable:** Works from any location with saved folder preferences
- **Minimal:** No excess files beyond the executable

## Installation & Usage

### Option 1: Auto-Detection (Recommended)

1. Download `Holy Lois Updater.exe`
2. Place it directly in your Minecraft installation folder (where you have `mods/` and `config/` folders)
3. Run the exe - it will automatically detect your Minecraft folder
4. Click "Check for Modpack Updates" to sync your mods and configs

### Option 2: Portable Mode

1. Download `Holy Lois Updater.exe`
2. Place it anywhere on your system
3. Run the exe - it will automatically use saved preferences or prompt for path
4. Click "Check for Modpack Updates" to sync

## What It Does

- Downloads and syncs mods from the official repository
- Updates resource packs
- Syncs configuration files
- Removes outdated mods automatically
- Shows detailed changelog of changes
- Automatically checks for updater updates on startup

## Auto-Detection Logic

The updater will automatically use the current directory as your Minecraft folder if both of these conditions are met:
- `mods/` folder exists
- `config/` folder exists

Otherwise, it will use saved config or default Minecraft path.

## Updater Updates

The updater automatically checks for updates to itself when started. If a new version is available, the main button will change to "Updater Update Available" to update the exe before proceeding.

## Distribution

Users only need the single `.exe` file. No DLLs, no config files required for basic operation. The exe is self-contained and includes all necessary dependencies.

## Technical Notes

- Built as a single-file executable for ease of distribution
- Compatible with Windows 11/10
- Requires .NET 9 runtime (automatically included in the exe)
- Saves user preferences to `config.json` in the same directory as the exe

## Version

Current: v1.1.0.0