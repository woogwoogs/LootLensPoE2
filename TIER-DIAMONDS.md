# Inventory tier diamonds

Appearance → Inventory Tier Diamonds controls visibility, size and each tier's color.
Defaults: T1 gold, T2 cyan, T3 purple.

Each normal explicit T1–T3 affix adds one diamond at the bottom-left of its bag item.
Three T1 affixes show three gold diamonds. Mixed tiers are ordered T1, T2, T3.
Hybrid affixes count once. Crafted, implicit, hidden and unique modifiers do not count.
Unknown tiers are omitted. These are actual modifier tiers, not roll percentages or
a guarantee that an item qualifies for your build.

Markers wrap on narrow items and stay separate from the existing green check.
They work without hovering and independently of analyzer/check visibility settings.
Tier data refreshes every 250 ms; icon positions are read each frame.
Inventory, stash, vendors, ritual rewards and other supported item windows are
enabled by default. Equipped gear has a separate option and is disabled by default.
For uncommon windows without a visible item-list API, the hovered item is marked.

Version 0.19.0. Requires runtime compilation in ExileCore2.
