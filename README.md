# WC-SARS
WC-SARS is a program that acts as a sort of game server for the video game [Super Animal Royale](https://animalroyale.com "Super Animal Royale's official site") ("SAR"). Specifically, this project aims to recreate the SAR v0.90.2 server (`SteamDepot ManifestID: 1635400125214728228`).

If you have never played SAR before, then you should give it a try. The game is free, easy to get into, and doing so will make this project make just a little bit more sense!


## Little Info/ Goals
The primary goal for this project is to recreate older versions of the official game servers. This is so others and me (WaffleIngester) can play older versions of SAR with their friends.

It is preferred to keep as many features as accurate to the original update as possible. However, inevitably, inaccuracies are going to occur. Whether this is because of spaghetti-code/ guessing how things are intended to work, or because keeping things accurate is really annoying. An example is intentionally keeping the ability to heal whilst moving around. In older SAR versions players had to sit still if they wished to drink or tape (similarly to emoting/ picking up downed teammates).

Furthermore, the primary game loop (safezones closing-in; Delivery Moles stopping by) is non-existent. Many other features are missing as well and may or may not be implemented in the future! This could be because it is too complicated to add at my skill level without any help, or simply because I have other things I'm interesting in doing. That or because devs kindly asked to stop :[
(thankfully this has yet to happen! wahoo!)

If you find an issue then you're free to open up an issue. **However,** it is appreciated if you verify your issue is unique and hasn't been reported already! Additonally, if you could please include steps to reproduce the isssue and any crash messages please do so!

If you wish to contribute to this heaping spaghetti pile then you may do so! There is no real formal... approach to doing so? If you beleive you have fixed something submit a pull request or something of the like!


# How to Use
(This section is a bit unfinished)...

First and foremost, WC-SARS is primarily a Windows-based application. If you are on Mac, Linux, or something else; then the program will either crash, or some important functions simply break. I have not tested any of these platforms (aside from Windows 10), so your mileage may vary!

---

WC-SARS can either be compiled by yourself in Visual Studio, or one can download a [Release](https://github.com/WaffleIngester/WC-SARS/releases) (usually new releases come out when new features are added).

You should now have a single folder "WC-SARS" (or "netcoreapp3.1") laying around. Within this folder should be `WC-SARS.exe` and another folder `datafiles`. At this point, you may simply run `WC-SARS.exe` and all [player data related](https://github.com/WaffleIngester/WC-SARS#player-data) files will be automatically generated if they do not already exist.

However, [some required files](https://github.com/WaffleIngester/WC-SARS#required-files) necessary for the program to run properly will NOT be automatically generated (due to their complex/ ever-changing nature). For more information about required files, please see the "Required Files" section below.

Upon launching the program with all necessary data files acquired, the program will either startup successfully or crash immediately. If the program fails to launch/ crashes with the following (or similar) message: `Unhandled exception. System.Net.Sockets.SocketException (10049): The requested address is not valid in its context.` then you are trying to bind to an invalid IP address.

__To fix this,__ simply locate `server-config.txt` then change `server-ip` to either `127.0.0.1` (localhost), or your computer's local IP address. To find your local address (Windows) simply open command prompt, type `ipconfig`, and then look for `IPv4 Address... xxx.xxx.x.xxx)` (typically in the format `192.168.1.xxx`).

To connect to the server the program is now hosting... one must figure that out on their own... Currently, the game must be modified so that it always connects to a specific IP address & sends [a certain NetMessage](https://github.com/WaffleIngester/WC-SARS/blob/master/WCSARS/Match.cs#L1374) to the server program correctly.

Currently, there is no external program to hook onto the game and execute the join-server function with an arbitrarily-provided ip-address... but if someone wants to make one... hook me up :]

# Required Files
There are a few files necessary for the program to work as intended. For some files, it is inconsequential if their data is filled out incorrectly (e.g. `banned-players.json`); however, some data types need their data files to be correct/accurate to that of the intended clients, otherwise desync will occur (e.g. `DoodadType`, `Tiles`, `Decals`).

To separate these two extreemes, files that are necessary only for WC-SARS server stuff is to be located within the same folder as `WC-SARS.exe`. Files that are necessary to build and create data specific for SAR types (like level data) are to be located in separate folder known as "datafiles".

What are "datafiles" exactly?
-
To put it simply, "datafiles" is the term I use to refer to all required data files from Super Animal Royale that utilize Unity's ["TextAsset"](https://docs.unity3d.com/Manual/class-TextAsset.html) asset type.

These TextAssets can range from storing data related to weapons & cosmetic items, all the way to stuff like the types of decals seen around the game world & even the map data itself. These assets are super versatile! As somewhat mentioned before, SAR relies on these files to build/ represent certain types correctly (example: weapons). So, if we want to be accurate as to how the game would build its weapons, doodads, map, level collision grid, etc. we have to use these files as well.

Well, actually, not EVERYTHING from these files is needed. Only the data that is actually used is required. Everything else aside from that is just taking up space. So, we don't necessarily need the files as-they-are, extracted from the game. We could instead use stripped-down/ customly written versions of them if we really wanted to. Which we do to some extent!

Where do required files go?
-
Most required files go in a subfolder known as `datafiles`. This folder should be located within the running location of `WC-SARS.exe` and `WC-SARS.dll`.

Some files (`server-config.txt`, `banned-players.json`, etc.) are simply found within the running location of `WC-SARS.exe`.

If none of this makes any sense, try downloading a release and looking at the folder structure for yourself. It is a bit hard to explain succinctly, and viewing it for yourself may prove more useful!

Or perhaps something like this? (don't mind the incorrect formatting too much)
```
-- File Explorer
	-- /WC-SARS/
		-- datafiles/
			-- mapdata.json
			-- datafiles/doodaddata.json
		-- wc-sars.exe
		-- bannedlist.txt
		-- admins.txt
```

More Information
-
Below you may find more small tidbits about each required file/ whether it is generated automatically or not.

For simplicity sake, the below  section is collapsed. If you wish to view it, just click the little arrow below!
<details>
<summary>Click to expand</summary>

## Server Config
**Must be placed in the same folder as `WC-SARS.exe`.**
(will be auto-generated if not found)

> **NOTE:** config file loading/ reading is a little busted, so be careful when feeding it invalid values!

Configuration of properties that the program will utilize when setting up Matches.

-- Details on each entry has been removed for the time being


## Player Data
**Must be placed in the same folder as `WC-SARS.exe`.**
(will be auto-generated if not found)

Currently this file just stores a bunch of player PlayFabIDs and whether to set their name to certain colors built into the game itself.

* `String` `playfabid`
    - This player's PlayFabID.
* `String` `name` **(UNUSED)** 
    - This player's name. (may use for resolving names)
* `Bool` `dev`
    - Makes this user's name dev-name colored (overwrites all other name-colors).
* `Bool` `mod`
    - Makes this user's name mod-name colored (dev color overwrites this).
* `Bool` `founder`
    - Makes this user's name founder-name colored (gets overwritten by mod-color). 

> Example `player-data.json` file:
```json
[
	{
		"playfabid": "0123456789ABCDEF",
		"name": "xX-EpicGamer42-Xx",
		"dev": false,
		"mod": true,
		"founder": true
	},
	{
		"playfabid": "FEDCBA9876543210",
		"name": "PPFF",
		"mod": true,
	}
]
```

## Banned Players
**Must be placed in the same folder as `WC-SARS.exe`.**
(will be auto-generated if not found)

A list of banned PlayFabIDs that will have their connections refused.
* `String` `playfabid`
    - Banned player's PlayFabID.
* `String` `name`
    - This banned player's name.
* `String` `reason`
    - The reason this player was banned. If blank, a default message will be provided.

> Example `banned-players.json` file:
```json
[
	{
		"playfabid": "0123456789ABCDEF",
		"name": "xX-EpicGamer42-Xx",
		"reason": "Unspeakable actions."
	},
	{
		"playfabid": "FEDCBA9876543210",
		"name": "I got banned for no reason...",
		"reason": ""
	}
]
```
## Banned IPs
**Must be placed in the same folder as `WC-SARS.exe`.**
(will be auto-generated if not found)

A list of banned IPs who will have their connections refused.
* `String` `ip`
    - Banned IP address.
* `String` `playfabid`
    - PlayFabID attached to this banned IP.
* `String` `name`
    - Name attached to this banned IP.
* `String` `reason`
    - The reason this IP was banned. If blank, a default message will be provided.

> Example `banned-ips.json` file:
```json
[
	{
		"ip": "127.0.0.0",
		"playfabid": "0123456789ABCDEF",
		"name": "xX-EpicGamer42-Xx",
		"reason": "Unspeakable actions."
	},
	{
		"ip": "0.0.0.0",
		"playfabid": "FEDCBA9876543210",
		"name": "AAAAAH",
		"reason": ""
	}
]
```


## Decals (_decals)
*NOTE: `decals.json` must be placed in the `datafiles` folder.*

This file contains all necessary information to define `Decals` for the program.

A version with **only** the necessary data is included with releases.

## Doodads (_doodads)
*NOTE: `doodads.json` must be placed in the `datafiles` folder.*

This is a file which contins all necessary information to define ``DoodadTypes`` for the program.

A version with **only** the necessary data is included with releases.

## Grass (_grass)
*NOTE: `grass.json` must be placed in the `datafiles` folder.*

This is a file which contins all necessary information to define ``GrassTypes`` for the program.

A version with **only** the necessary data is included with releases.

## EarlyAccessMap1 (EarlyAccessMap1)
*NOTE: `earlyaccessmap1.txt` must be placed in the `datafiles` folder.*

This file contains ALL of the information about the overworld / current level.

## Tiles (_tiles)
*NOTE: `tiles.json` must be placed in the `datafiles` folder.*

This file contains all necessary information to define `tiles` for the program.

A version with **only** the necessary data is included with releases.

## Weapons (_weapons)
*NOTE: `weapons.json` must be placed in the `datafiles` folder.*

This file contins all necessary information to define ``WeaponTypes`` for the program.

A version with **only** the necessary data is included with releases.

</details>

# Libraries & Extra Credits
Libraries used (big thanks!):

* Markus Göbel (Bunny83) -- [SimpleJSON](https://github.com/Bunny83/SimpleJSON)
	- Allows for loading JSON data from important data files.
* Michael Lidgren -- [LidgrenNetwork](https://github.com/lidgren/lidgren-network-gen3)
	- Networking library used by SAR; and so our lives are easier if we do too

And thank the SAR devs for still not asking me to take this down ❤ (yet D:)