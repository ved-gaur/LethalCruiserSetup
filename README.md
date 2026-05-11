# CruiserSetup

CruiserSetup adds a `/setup` chat command for arranging tools on the Company Cruiser.

It is intended for players who want to quickly stock the Cruiser after reloading a save file, or a sell-day.

## Features

- `/setup` command for moving tools onto the Company Cruiser
- Preset-based layouts
- Per-tool placement configuration
- Per-tool min/max counts
- Repositions tools already on the Cruiser
- Moves unusable Cruiser tools, such as empty shotguns or empty weed killer, to a configured discard pile

## Requirements

- BepInEx
- ChatCommandAPI

---
## Usage
**The cruiser must be magnetised to run the command**

Type into chat:

```config
/setup
```

To use a configured preset:

```config
/setup PresetName
```

To configure a preset, add to the config file the following section template:

```config
[Preset.PresetName]

## Format: x,y,z,min,max
## x,y,z = Cruiser placement position
## min = minimum number to leave on the ship
## max = maximum number to take/keep on the Cruiser
## Use * for infinite max
## Use disabled to skip an item

Walkie-talkie = x,y,z,min,max
Flashlight = disabled
Shovel = x,y,z,min,max
Lockpicker = x,y,z,min,max
Pro-flashlight = x,y,z,min,max
Stun grenade = x,y,z,min,max
Stun grenade-used = x,y,z,min,max
Boombox = x,y,z,min,max
TZP-Inhalant = x,y,z,min,max
Zap gun = x,y,z,min,max
Jetpack = x,y,z,min,max
Extension ladder = x,y,z,min,max
Radar-booster = x,y,z,min,max
Spray paint = x,y,z,min,max
Weed killer = x,y,z,min,max
Shotgun = x,y,z,min,max
Shotgun-1 = x,y,z,min,max
Kitchen knife = x,y,z,min,max
Key = x,y,z,min,max
```

---

## Cruiser Position Guide

Tool positions use `x,y,z` coordinates relative to the Cruiser.

#### Back shelf, inside the cruiser
Use `y = -0.20` and `z = 0.40`, with `x` anywhere from `-1.00` to `1.00`. 

#### Side shelves, inside the cruiser
- Use `x = -1.00` for the left side or `x = 1.00` for the right side. 
- Use `y = 1.15` for the top shelves and `y = 0.35` for the bottom shelves. 
- Keep `z` between `-0.55` and `-2.60`. More negative `z` values are further forward. 

#### Example positions
- `Boombox = 0.00,-0.20,0.40,0,*` puts the boombox in the middle of the back shelf
- `Pro-flashlight = -1.00,1.15,-0.60,0,3` puts pro-flashlights on the left top shelf near the back
- `Shovel = 1.00,1.15,-2.30,0,4` puts shovels on the right top shelf further forward
- `Spray paint = -1.00,0.35,-1.60,0,4` puts spray paint on the left bottom shelf.
