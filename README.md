# Playnite Wii/GameCube GameTDB Metadata Extension

Adds support for loading [GameTDB](https://www.gametdb.com/) metadata for Nintendo Wii and Nintendo GameCube to [Playnite](https://playnite.link/).

## Installation

[Download the .zip](https://github.com/uwx/PlayniteDolphinMetadata/releases), extract to your Playnite folder (such as `%APPDATA%\Playnite`). .pext version coming soon!

You have to restart Playnite after installing the extension.

## Usage

### For ROMs using the .RVZ or .WAD format

1. First, run Dolphin Emulator at least once. It must be configured with your .RVZ or .WAD games visible.
2. In your Playnite settings, go to Metadata Sources > GameTDB (Dolphin).  
   ![image](https://user-images.githubusercontent.com/13633343/120955408-acb30f00-c727-11eb-93e8-428d5845600e.png)
3. Set the path to your Dolphin Emulator user folder, which is usually `Documents\Dolphin Emulator`  
   ![image](https://user-images.githubusercontent.com/13633343/120955496-e08e3480-c727-11eb-990e-9b60b1bc7e66.png)
4. Your .RVZ and .WAD-format ROMs will have metadata downloadable in Playnite.  
   This support is experimental, so let me know if you have any issues.

Use Edit Game Details > Download Metadata to download with GameTDB as a metadata source. You can also configure it to be used on all metadata
downloads by going to Settings > Metadata.

### For ROMs using the .ISO, .CISO, .WBFS, .GCZ, .WBI, .WDF, .WIA, or .FST format

These will work out of the box, so simply use Edit Game Details > Download Metadata to download with GameTDB as a metadata source. You can also
configure it to be used on all metadata downloads by going to Settings > Metadata

## Configuration

You can choose the preferred language and cover art style to be downloaded in the extension settings. If a cover is not found with the matching
parameters, the extension will attempt to find any cover.
