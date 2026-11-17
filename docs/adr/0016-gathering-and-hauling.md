# 0016 — Gathering and hauling

Status: accepted (2026-11-16). First step of the "explore and clearly bring things back" stretch.

## Decisions

- **Gatherable items.** An item may carry `gather: { count: [min, max], regrow_days: n }`. Gear cannot.
  New items: wild berries, medicinal herbs, firewood, scrap metal.
- **Spots are identified by presentation, remembered by the simulation.** `Gather { SpotId, ItemId }`
  yields a seeded count into the pack (on a trip) or the stores (at home) and records the day in
  `SimState.Gathered`. The spot is picked over until `regrow_days` pass. This survives save and load, so a
  picked bush is still bare after loading. Spot ids are stable names the scene builder assigns
  ("berries_007"); renaming them in a rebuild forgets which were picked, which is acceptable for greybox.
- **New games start with supplies** (`NewGameOptions.StartingStores`, set on SimHost): jerky, cornmeal,
  cartridges, bandages and firewood.
- **Interaction is one pattern.** `Interactable` (Bridge) is anything you walk up to and press E on;
  `Interactor` (Game, on the player) picks the nearest one in front, shows its prompt, and shows the result
  line. Homestead buildings, beds and the infirmary will use the same pattern.
- **The load is visible and felt.** `PackLoad` shows the pack on the back during a trip, scales it with fill,
  adds a sack and a lashed bundle as it gets heavy, and slows walking and running from half full, a lot
  when overloaded (tuning values serialized).
- **The stores are visible.** `StoresDisplay` on the stores shed turns the household inventory into props on
  the shed's floor and shelves, by item id or tag (firewood stacks, berry baskets, herb bundles, scrap,
  food sacks, crates), rebuilt whenever the stores change.

## Not done

- Gathering is instant; no animation or time cost.
- Hunting (carcasses, butchering) waits for shooting.
- Items in the shed are not individually interactive.
