# 0007 — Homestead: structures, jobs, food, morale and trade (first pass)

Status: accepted as defaults, built on request without spec review (2026-11-16). Numbers are first
guesses in `HomeRules`; costs and yields are in content.

## Decisions

- **Structures** are the nine slots from the design, as content. Each has tiers with an item cost and a
  set of homestead totals it provides. Building is instant once the cost is paid. A new homestead is a
  tent and a fire pit.
- **Homestead totals** (`HomeStat`) are summed from built structures and from people doing jobs:
  capacity, morale, treatment, defense, workshop, preserving, stabling, hygiene, warding. Only capacity,
  morale, treatment and defense have consequences so far.
- **Jobs** are content. Anyone in the household aged 13 or more can hold one, except the head. Work is
  resolved once per season (`PassSeason`), and the result is kept as the last home report and raised as
  `HomeReported`, for the scene where the head returns. Yield scales with the worker's skill and the
  structure's tier. The skill grows 0.25 a season, to 5.
- **Food.** Everyone but the head eats 2 food items a season from the stores. The head is assumed to eat
  on the road.
- **Morale** (0..100) moves at most 10 a season toward a target: 50, plus 5 per point of morale the
  homestead provides, minus 10 per hungry member, minus 5 per person over capacity.
- **Opinion of the head** rises in a happy house (morale 60+) and falls in a miserable one (below 25).
  In a miserable house, adult wards and sworn members who think badly of the head have a 25% chance a
  season to leave; half of those take something with them. Blood and partners do not leave.
- **Home treatment** (`TreatWoundAtHome`) uses the infirmary tier that has been built and the best
  Medicine among the others at home. The older `InfirmaryTreatWound`, with an explicit tier, remains for
  doctors elsewhere.
- **Trade** (`SellItems`, `BuyItems`) needs the settlement's general store to be open. Selling pays the
  item's value; buying costs double. Every 5 coin of trade pushes the town's prosperity by 1 at the
  season's turn, which is the first real link from the homestead to the town.
- **Saves are canonical.** State dictionaries became sorted, after a test showed that two equal states
  could serialize with different key order.

## Not done

- Work is seasonal only. Days spent away on a trip do not produce anything, and there is no "return
  home" moment in the sim; presentation should show the last report when the head arrives.
- Construction time, and the structures' own effects beyond the four totals above (workshop repairs,
  preserving against spoilage, stabling and carrying, hygiene, warding against the Thin).
- Companions on trips, cohesion between members (only opinion of the head is tracked), disputes as a
  system (one event covers it), elders as mentors.
- Recruits and births are not refused when the dwelling is full; crowding only costs morale.
