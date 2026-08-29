# LootLens 2

ChatGPT generated readme ¯\(ツ)/¯

LootLens 2 is an ExileCore2 item-analysis overlay for Path of Exile 2. It keeps the compact, minimal charcoal presentation of the original PoE1 LootLens while using PoE2's own modifier records.

## What it shows

- A compact analysis strip attached directly to the native item tooltip without repeating the native item name, base, rarity, requirements, or item stats.
- One subtle metadata line such as `iLvl 35 | 5 Mods | PDPS 170.0 | EDPS 52.5 | DPS 222.5` before the modifier rows.
- Every prefix, suffix, unique, and useful hidden weapon modifier in a clean color-coded block.
- The real PoE2 modifier tier in an unboxed right-hand label, retaining useful context such as `T8/9`.
- Thin stat-colored roll lines with outlined diamond markers for immediate roll-quality recognition.
- Base-aware ranges: tiers that cannot roll on the item's base type are excluded.
- A LootLens 1-style overall perfection section for unique items with a diamond marker, percentage, variable-roll count, variable-mod count, and unique drop tier. Fixed rolls are excluded.
- Database-backed unique drop labels such as `T1 UNIQUE`, `BOSS UNIQUE`, and `LEAGUE UNIQUE`.
- Weapon PDPS, EDPS, and total DPS calculated from the final damage shown by the native tooltip and presented as clear labeled summary metrics.
- Final displayed Armour, Evasion, Energy Shield, and Runic Ward totals on armour and shields; zero-value defence columns are omitted.
- One small green check in the top-left of qualifying inventory and stash items.
- Passed qualifying thresholds only in a slim single-line footer with small drawn green dots and no footer sliders.
- LootLens 1-style single-line modifier rows grouped as prefixes first and suffixes second, with display-only shorthand, tiny affix letters, stat-family colored accents, and optional fully colored modifier names. Hidden, crafted, and unique names retain special colors.
- Semantic color matching distinguishes related families with separate shades and prioritizes the stat actually being modified over incidental words elsewhere in the modifier.
- A selectable Detailed layout retaining the previous exact ranges, percentages, and expanded item summary.
- An HC Campaign IFL mode with one acceptable-good baseline for every campaign stage and supported gear slot.

## The green check

This intentionally replaces LootLens's old 1/2/3-star rating.

HC Campaign mode is enabled by default. The item earns one green check when it meets the acceptable-good baseline for the active campaign stage. There are no stars or multiple quality bands.

Automatic stage selection follows the current character level. It can be manually locked to Act 1, Act 2, Act 3, Act 4, Interlude I, Interlude II, Interlude III, or Early Maps. Every active baseline can be inspected under `CHECK RULES`.

The rules use slot-specific combinations instead of treating every stat equally:

- Standard armour, jewelry, and quivers need two qualifying stats from Life, individual elemental resistances, all-elemental resistance, Chaos resistance, and Armour applying to Elemental Damage.
- Boots must meet the movement-speed requirement plus two qualifying stats from the standard gear pool.
- Shields require both a flat Armour modifier and an increased Armour modifier.
- One-handed maces require `+# to Level of All Melee Skills`.
- Martial weapons and bows must meet total DPS before secondary offensive stats complete the check.
- Caster weapons must meet spell damage or skill-level power plus one other enabled caster stat.

The original editable threshold profiles remain available through Custom mode. Set any custom threshold to `0` to disable it.

Included profiles:

- Helmet
- Body Armour
- Gloves
- Boots
- Shield
- Belt
- Ring
- Amulet
- Quiver
- 1H Mace
- Martial Weapon
- Bow
- Caster Weapon


