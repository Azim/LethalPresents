# LethalPresents
A mod which gives Presents a chance of spawning a random enemy when opened.

# Configuration
In your BepInEx/config/LethalPresents.cfg the following options are present:

`SpawnChance`, to configure the probability of enemies spawning;

`EnemyBlocklist`, list of enemies to remove from the spawn pool, see game logs for available options;

`IsAllowlist`, turns blocklist into allowlist, so that only enemies from that list can spawn. Spawned enemies are still filtered by map and inside/outside, so make sure to provide at least one entry for factory, mantion and outside;

`ShouldSpawnMines`, to enable/disable spawning of the mines;

`ShouldSpawnTurrets`, to enable/disable spawning of the turrets.

# Releases

### 1.0.9
* Version bump for more better v66
* Added workaround for mods which incorrectly mess with currentLevel's Enemies, OutsideEnemies and DaytimeEnemies arrays. If you find such mods, please complain to their developers.

### 1.0.8
* Version bump for more better v60
* Trying out AutoHookGenPatcher

### 1.0.7
* Fixed a crash on enemies which do not correctly fill out their properties >:/\
*such enemies will not be spawnable through the mod*
* Removed ~~Herobrine~~ AprilCompany 

### 1.0.6
* Fixed the debug message spam in console (im sorry alright)

### 1.0.5
* Bees are now properly spawned alongside other outside enemies

### 1.0.4
* Moved from Harmony to Monomod
* Fixed Blacklist/Whitelist not working properly

### 1.0.3
* Fixed turrets and mines not spawning when there are no regular enemies available for spawn
 
### 1.0.2
* Added config
* Added mines and turrets spawning
 
### 1.0.1
* Fixed spawn chance being 100%
 
### 1.0.0
* Initial release