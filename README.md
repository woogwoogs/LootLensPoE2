# LootLens2

A compact item mod analyzer for **ExileCore2 / Path of Exile 2**.

## Features

- Shows modifier values, tiers, and prefix/suffix indicators.
- View rolls as **sliders** or **numerical ranges**.
- Switch between **Current Tier** and **All Tiers** ranges.
- Weapon DPS breakdown and unique-item perfection.
- Adjustable text size, colours, and panel placement.
- **Hold** a hotkey to view the analyzer, or use **Always** mode.

## New: Tier markers

Small diamonds let you spot high-tier mods directly on items without hovering.

- **T1 = purple · T2 = blue · T3 = green** by default.
- One diamond per qualifying affix: three T1 affixes show three purple diamonds.
- Adjustable colours and size (default: **12**).
- Enabled for inventory, stash, vendors, Ritual, and other supported item windows. Equipped gear is optional.

## Roll ranges

**Current Tier** shows how the roll compares within its own tier. **All Tiers** compares it against the full range across valid tiers for that item base.

![Roll range options: sliders and numbers, current tier and all tiers](docs/roll-range-options.png)

## Install

Place the `LootLens2` folder in `ExileCore2/Plugins/Source`, replacing the previous folder if updating. Enable the plugin and adjust its settings. The default analyzer hotkey is **F8**.
