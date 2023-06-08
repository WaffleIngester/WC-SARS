# WC-SARS Tools
Included here are some simple scripts/ tools that you can use for particular *datafiles*.

It isn't much to write home about, but if you find any of these useful then yay.

(maybe more in the future!)


## JSON-Minify-X.py
These are a set of very simple Python scripts that take a particular raw data file, reads it, and exports only the necessary data for the program to run. These data files themselves, coming from the game.

The scripts should be straightforward, so only really important things to note will be mentioned below.


#### Decals Minify
Due to the simple nature/ laziness when creating the scripts, some problems may occur. For whatever reason it appears that the double-quotation-marks trips up the parsing. A workaround (other than just fixing whatever is wrong with the script) is to toss the raw `_decals.json` file into a JSON-formatter then load that re-formatted file into the script.