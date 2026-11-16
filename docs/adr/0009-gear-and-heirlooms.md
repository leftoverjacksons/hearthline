# 0009 — Gear: unique items, equipment slots, ledgers and heirlooms (first pass)

Status: accepted as defaults, built on request without spec review (2026-11-16).

## Decisions

- **Two kinds of item.** Goods are counts (jerky, lumber). Gear is any item definition with `equip`;
  each piece is its own object (`GearItem`) with an id, a ledger, and an optional engraved name. The
  design's "every unique item keeps a ledger" is this.
- **Gear lives on a person or in the stores.** A character's gear list is part of that character, so it
  travels with them and stays with them when they die.
- **Ten slots**: the seven outfit layers from the design plus long gun, sidearm and knife. One piece per
  slot; equipping replaces what was there. Only worn gear has effects.
- **Bridge contract.** `Simulation.GearOf(character)` returns each piece with its slot, a `visual` asset
  key and whether it is worn; `GearChanged` says when to redraw. Like marks, the outfit is rebuilt from
  state, so it survives save and load.
- **Ledger.** Written automatically when gear is created, changes hands, is put away or named, and by
  events (`gear:` outcome note) or presentation (`NoteOnGear`, for kills and close calls).
- **Bequeathing** is taking gear from someone dead (`TransferGear`). The ledger and the taker's chronicle
  record it.
- **Heirloom.** When gear is in the hands of a blood descendant of its first owner, its
  `heirloom_effects` apply as well. This is recomputed each time it changes hands.
- **Naming** needs three ledger entries, a workshop, and the head at home.

## Not done

- The heirloom bonus is on or off; it does not grow with the length of the ledger as the design suggests.
- Condition, fouling, repair, and the workshop's part in them.
- Gear has no carried weight, cannot be bought or sold, and is not offered for bequest automatically at
  succession; the player (through UI) moves it.
- Outfit properties from the design (warmth, weather, noise, how factions read you) beyond plain effects.
- Nothing in Unity draws gear yet. That waits on the character pack and its sockets.
