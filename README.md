## WC-SARS
---

WC-SARS is a program which acts as a gameserver for the video game [Super Animal Royale](https://animalroyale.com "Super Animal Royale website"). If you haven't played it before, then you should give it a try.

A lot of features are missing and may or may not be implemented in the future. If you try and make sense of anything going on you will surely cry-- but that's OK because if you head empty everything is OK!


---
# Required Files
There are a few important files required for the program to run. Some are found in the same location as the `WC-SARS.exe` executable, while others are found in a subfolder of this location known as `datafiles`. If the `datafiles` folder does not exist, you can simply create it in the correct location and place the required files within.

## Config
> NOTE: `config.json` must be placed in the same folder as `WC-SARS.exe`.

> Presently, there is not a whole lot to the config file. For now, the only thing(s) available are `ServerIP` and `ServerPort`.
* `ServerIP` the IP address which the program will attempt to bind to.
* `ServerPort` the port which the program will attempt to bind to.

## Player Data
> NOTE: `player-data.json` must be placed in the same folder as `WC-SARS.exe`.
This file is auto-generated if not found.

> This file was indented to hold the data of all players who may connect to the server. Currently, however, it is just a list of players and tags to give them colors built into the game.
* `String` `playfabid`
    - This player's PlayFabID.
* `String` `name`
    - (UNUSED) This player's name. (may use for resolving names)
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
		"name": "notice I can remove 'dev' and founder/mod is true",
		"mod": true,
		"founder": true
	}
]
```

## Banned Players
> NOTE: `banned-players.json` must be placed in the same folder as `WC-SARS.exe`.
This file is auto-generated if not found.

> This file holds a list of all banned PlayFabIDs for the server to load.
* `String` `playfabid`
    - Banned player's PlayFabID.
* `String` `name`
    - (UNUSED) This banned player's name.
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
		"name": "Notice you can just leave reason blank?",
		"reason": ""
	}
]
```
## Banned IPs
> NOTE: `banned-ips.json` must be placed in the same folder as `WC-SARS.exe`.
This file is auto-generated if not found.

> This file holds a list of all banned PlayFabIDs for the server to load.
* `String` `ip`
    - Banned IP address.
* `String` `playfabid`
    - (UNUSED) PlayFabID attached to this banned IP.
* `String` `name`
    - (UNUSED) Name attached to this banned IP.
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

## WeaponData
> NOTE: `weapondata.json` must be placed in the `datafiles` folder.

> This is a file which contins all necessary information to define ``WeaponTypes`` for the program. A version with **only** the necessary data is included.

## DoodadData
> NOTE: `doodaddata.json` must be placed in the `datafiles` folder.

> This is a file which contins all necessary information to define ``DoodadTypes`` for the program. A version with **only** the necessary data is included.

## MapData
> NOTE: `earlyaccessmap1.txt` must be placed in the `datafiles` folder.

> This file contains ALL of the information about the overworld / current level. This file is included with releases.

---
# Extra Credits
> Wish to say "thank you" to these individuals for creating the libraries this program uses:

* Markus Göbel (Bunny83) -- [SimpleJSON](https://github.com/Bunny83/SimpleJSON)
* Michael Lidgren -- [LidgrenNetwork](https://github.com/lidgren/lidgren-network-gen3)

---
# Final Notes
While the "Master" branch is mostly stable, bugs still creep in. If you want the latest changes please compile "test1" instead. As this branch will likely have fixes already for new features; along with the new features being added.