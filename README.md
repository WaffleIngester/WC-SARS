## WC-SARS
### "It is like a server, but it's not!"
Hello this is my little program which acts as a gameserver that you can connect to in the game [Super Animal Royale](https://animalroyale.com "Super Animal Royale"). After 3 years of playing I got kind of bored and thought it would be fun to try and make a server program like this as I have never done anything of the sort before.

If anyone from Pixile wants this goners then just reach out. I don't think I care too much about it going bye-bye to not.

------------

### Known Issues
There are **a lot** of issues with the program. So much so, that I can categorize them! Isn't that wonderful?

+ General Problems
	+ Spaghetti code
		+ I may have pasta maker in my DNA, but this ain't the good kind of pasta. This is like baby's first pasta. What I'm trying to say is, this application just does what I wanted it to do and nothing more. If it got the job done, then that was it and it was onto the next part to goof up.
	+ The server relies on a modified client
		+ Because of shenanigans, it was easier to just modify what the client wants from the server at points, rather than have the server send stuff correctly. (those which were, are clearly noted*)
		+ *probably forgot to note them :]
	+ If two people connect at the same time the loading players get glitched
		+ It's more than likely just because of how the server deals with incoming connections is an absolute mess.
	+ Server has no hecking clue where spawn-tiles are >> Player Issue
		+ Repeated in Player Issues; server has no clue where players should properly spawn.
		+ No clue where/how many Loot; Coconut; and also Vehicle, SpawnTiles there are. Look, it's complicated. Right now this isn't a major issue. Keep in mind, however; playing on a different version of the game where there's more or less of certain tiles **is going to desync the server and client.** No ifs or buts, it just will.
	+ Server doesn't really check client info
	 	+ Some methods do check... sort of, but the vast majority do not. This kind of... uh uh stinks! D:
	+ Barrels kind of... don't work
		+ Messed with barrels at the beginning of development, but couldn't be bothered to figure it out.
	+ Server doesn't keep track of player ammo/ammo in gun/reloading!
		+ Eventually™
	+ Server config is a bit scuffed
		+ Once again just stitched together like most other parts of the server.
	+ Server startup a bit broken
		+ It is just creating different "Match" objects. Which, again the creation is kind of weird/convoluted.
	+ Some other general problem
+ Player Issues
	+ Player has to use a modified client
		+ Already mentioned, but this really is a problem
	+ Without a modified client, server has no clue what player's name is
		+ The theory on how to fix this properly bothers me greatly. Not because it is too terribly difficult, in theory. Just what must be done is too morally reprehensibly and has the chance to be used for much more bad than good. I don't quite like that, and I hope others understand my wishes to not pursue this idea further. (Altho didn't really say what that is. Take a guess. )
		+ As such, the plan is to just **store player names in the PlayerData folder the server uses.** Player names must be put in *manually*, but this is the next best solution without having to make serious modifications or do things I think is too far.
	+ Server likely does not track player-ping properly
		+ Probably could get working just fine, I am just incredibly stupid
	+ Players can dance indefinitely in hamsterballs
		+ This one was just too funny to fix
		+ Well, it was funny and also having to implement a system to keep track of when a player moves is kind of far beyond my intelligence level
	+ Players cannot slip on bananas
		+ Same reason as emotes don't stop when you move. Literally have no clue how to implement this stuff in a console application.
	+ Players don't stop emoting on their own.
		+ Similar issue to being able to dance in hammerballs. Just couldn't be bothered to keep track of the emote
	+ Players cannot be hurt by skunk gas/skunk bombs/grenades/barrels
		+ Starting to notice a theme? Very difficult to figure out if a particular player is within range of these things. Even so, how much damage to actually give? That's a mystery that's probably never going to get solved :3
+ Loot / Weapon Issues
	+ Loot can desync
		+ SAR loot gen is weird. It's not that hard to goof something up and tell the client to generate some loot item that has glitchy properties.
	+ Server's loot class is absurdly obtuse
		+ No clue how to manage memory in C#. Some genius thought it'd be a swell idea to limit the amount of variables that are in a LootItem object. So this means that some properties have multi-uses. This absurd use of variables is most notable for GiveAmount which seemingly flip-flops in its purpose. (Is it really the item give amount, or is it some other data property?)
	+ Throwables can be duplicated
		+ Because the Player object's Inventory is just so weird, grenades are too. Handling when a grenade thrown was already weird, but the server must keep track of how many the player owns as well; and that is by using the Throwable's GiveAmount property. This leads to some potentially crazy stuff. One of which turns out to be duping throwables. No clue how or why. It sure is fun though!
	+ Guns can be duplicated
		+ Pretty sure all loot items in some way can be duped. Once again, no real idea as to why this is possible. **Working theory as to why items can be duplicated** is that the client can send multiple requests to get a certain loot item, and the server just takes it. Potential fix is to have a small cooldown on taking requests for a certain item type. Just going off LootID isn't really thesible, because that's ever-changing 
	+ Weird health juice LootItem junk
		+ Obtaining the remaining amount of health juice needed to reach max, will glitch out the spawning of the new health LootItem.
	+ Managing LootItems as a whole is a complete mess
		+ Ammo LootItems aren't even dealt with

------------

## Required Files Information
There are a few files which the server requires to actually run and do certain things. Most ~~are~~ will be located within the **/datafiles** subfolder (location = /path/to/program/**datafiles**). Others are just in the same folder as the program executable.

The required files in DataFiles, for the most part, are files found within the game itself. You have to get them yourself to use to actually run the server, sorry. But if you are able to get this program running/actually connect in game, then you are likely able to do that.

Another thing I wish to talk about in this section is the actual version of the game this program was designed for. It was designed for SAR v0.90.2. If you own the game you can actually get this version, if not then well you can't so I'm not sure what to tell you. You're likely using Steam depots though, so the ManifestID is 1635400125214728228. If this don't worky then uh whoops.

------------

### WeaponData | DataFiles Folder
This is a required file which contains all the formation about every weapon in the game. This data is used by the server to generate a list of all weapons that is then used in several important functions. Aside from the server having some issues figuring out how a Player should be damaged, LootItem generation also gets absolutely messed up. Which is likely to be the first thing you'll notice. If you're missing this file, or there's some issues with it as a whole, you're probably going to be encountering these issues.

------------
### PlayerData.json | ExecutableLocation
A JSON file which contains information about players which the server can use. As of now, data must be put in manually. Use a JSON validator to make sure everything is all good. However, as of now this is how the PlayerData file is made as so:

```
	{
	    "PlayerData":[
	        {
	            "PlayerID": "0123456789ABCDEF",
	            "Admin": true/false,
	            "Moderator": true/false,
	            "Founder": true/false
	        }
	    ]
	}
```
The PlayerData file is a bit weird, with there being another key called "PlayerData" just kind of there, but that's just the way it is!

PlayerID - PlayFabID of this player
Admin - Admin color tag
Moderator - Moderator color tag
Founder - Founder color tag

If no 'ColorTag' is specified, then player just has a white name. Right now a player being a Admin/Mod/Founder doesn't mean anything. All they get is a differently colored name. Pretty cool, right?

------------

### Config.json | ExecutableLocation
The server's configuration! This file means **absolutely nothing.** As of now all this is used for is so that hard-coded nor running the program with arguments has to be used. Just an easy-to-modify text file. More to come... maybe?

Current format:

```
config.json
{
    "ServerIP": "127.0.0.1",
    "ServerPort": 42896
}
