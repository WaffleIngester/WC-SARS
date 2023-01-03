# WC-SARS

WC-SARS is a program which acts as a gameserver for the video game [Super Animal Royale](https://animalroyale.com "Super Animal Royale website"). If you haven't played it before, then you should give it a try.

Many features are mising and may or may not be added in the future.

# Required Files
There are a few important files required for the program to run. Some are found in the same location as the `WC-SARS.exe` executable, while others are found in a subfolder of this location known as `datafiles`. If this folder doesn't exist, you can simply create in in the correct location and place the files within.

## Server Config
*NOTE: `server-config.txt` must be placed in the same folder as `WC-SARS.exe`.*
(this file is auto-generated if it's not found)

Properties that the Match's the program makes, will use. Some are required, such as `server-ip`, while others like `drink-rate` are for you to mess around if you wish.

Config generation is currently a bit wonky. If a key is not found then the default values is used. However the key won't get written back to the file again, so be cautious.

* `server-ip` `string`
    - IP Address the program will try to bind to.
    - Defauts to `127.0.0.1` / `localhost`.
* `server-port` `int`
    - Port the program will try to bind to.
    - Defaults to `42896`.
* `server-key` `string`
    - Key server uses to verify if clients can connect or not.
* `use-config-seeds` `bool`
    - Whether to use the seeds set in the config or randomly generate them on startup.
    - Defaults to `False`. (**Note:** All seeds default to `0`)
* `seed-loot` `int`
    - The seed to use for generating LootItems.
* `seed-coconuts` `int`
    - The seed to use for generating Coconuts.
* `seed-hamsterballs` `int`
    - The seed to use for generating Hamsterballs.
* `max-players` `int`
    - Maximum amount of Players that can join the Match.
    - Default is `64`.
* `lobby-time` `float`
    - The amount of time to spend in lobby (in seconds).
    - Defualts to `120 seconds`.
* `molecrates-max` `short`
    - Maximum amount of Molecrates that can spawn in the match.
    - Default is `12`.
* `dart-ticks-max` `int`
    - Maximum number of dart-ticks a Player can have.
    - Default is `12`.
* `dart-tickrate` `float`
    - Rate (in seconds) at which a Player can take dart damage.
    - Default is `0.6 seconds`.
* `dart-poisondmg` `int`
    - The amount of Poison Damage to do on a dart-tick damage attempt
    - Default is `9`, but this should be phased out by storing this in the weapon data.
* `skunkgas-rate` `float`
    - Rate (in seconds) at which a Player will take skunk gas damage.
    - Default is `0.6 seconds`, but is currently unused.
* `heal-per-tick` `float`
    - The amount of HP to heal a Player while they're drinking.
    - Default is `4.75 HP`.
* `drink-rate` `float`
    - Rate (in seconds) at which a Player can heal at.
    - Default is `0.5 seconds`.
* `campfire-heal` `float`
    - The amount of HP that a Campfire will give during a Campfire heal attempt
    - Default is `4 HP`.
* `campfire-heal-rate` `float`
    - Rate (in seconds) at which a Player can be healed by a Campfire.
    - Default is `1 second`.
* `coconut-heal-base` `float`
    - The amount of HP that a Coconut will give when eaten.
    - Default is `5 HP`.
* `safemode` `bool`
    - Whether the Match should run in "Safemode" or not.
    - Defaults to `True`, although has no real impact right now.
* `debugmode` `bool`
    - Whether or not to run in "Debug Mode"
    - Defaults to `False`. (does nothing right now)


## Player Data
*NOTE: `player-data.json` must be placed in the same folder as `WC-SARS.exe`.*
(this file is auto-generated if it's not found)

Currently this file just stores a bunch of player PlayFabIDs and whether to set their name to certain colors built into the game itself.

* `String` `playfabid`
    - This player's PlayFabID.
* `String` `name`
    - **(UNUSED)** This player's name. (may use for resolving names)
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
*NOTE: `banned-players.json` must be placed in the same folder as `WC-SARS.exe`.*
(this file is auto-generated if it's not found)

A list of banned PlayFabIDs that will have their connections refused.
* `String` `playfabid`
    - Banned player's PlayFabID.
* `String` `name`
    - **(UNUSED)** This banned player's name.
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
*NOTE: `banned-ips.json` must be placed in the same folder as `WC-SARS.exe`.*
(this file is auto-generated if it's not found)

A list of banned IPs who will have their connections refused.
* `String` `ip`
    - Banned IP address.
* `String` `playfabid`
    - **(UNUSED)** PlayFabID attached to this banned IP.
* `String` `name`
    - **(UNUSED)** Name attached to this banned IP.
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
*NOTE: `weapondata.json` must be placed in the `datafiles` folder.*

This file contins all necessary information to define ``WeaponTypes`` for the program. A version with **only** the necessary data is included.

## DoodadData
*NOTE: `doodaddata.json` must be placed in the `datafiles` folder.*

This is a file which contins all necessary information to define ``DoodadTypes`` for the program. A version with **only** the necessary data is included.

## MapData
*NOTE: `earlyaccessmap1.txt` must be placed in the `datafiles` folder.*

This file contains ALL of the information about the overworld / current level. This file is included with releases.

# Extra Credits
Wish to say "thank you" to these individuals for creating the libraries this program uses:

* Markus Göbel (Bunny83) -- [SimpleJSON](https://github.com/Bunny83/SimpleJSON)
* Michael Lidgren -- [LidgrenNetwork](https://github.com/lidgren/lidgren-network-gen3)

# Final Notes
While the "Master" branch is mostly stable, bugs still creep in. If you want the latest changes please compile "test1" instead. As this branch will likely have fixes already for new features; along with the new features being added.