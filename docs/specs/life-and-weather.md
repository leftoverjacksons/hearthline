# Spec: life and weather (M2)

Status: **approved 2026-11-17** (Travis). **Built** (ADR 0025): `WeatherTests`, `WeatherPlayTests`, and three weather events (`creek_in_flood`, `shelter_from_the_storm`, `fog_on_the_ridge`).

## Goal

The land should feel alive and changeable: weather that rolls in and stays a while, birds in the trees,
insects over the water, fireflies at dusk. All of it supports the tone of the land (DESIGN, *Exploration*):
lonely, natural, and quiet in a way you notice. Wildlife is also a tell. **Birds fall silent and keep away
from Thin places** (DESIGN, *Art*: "silent birds").

## Scope

**In:**
- **Weather in the simulation:** clear, overcast, fog, rain and storm. It is seeded by season, changes once
  or twice a day, and is saved with the game.
- **Events can depend on it:** `weather_any` in an event's trigger, plus the weather in the storyteller's
  context.
- **What weather looks like:**
  - rain that follows the camera;
  - ground and leaves darkening as they get wet;
  - cloud cover dimming the sun;
  - fog thickening;
  - wind bending the trees harder;
  - lightning flashes in a storm.
- **Ambient life (presentation only, nothing saved):**
  - birds perched in trees that scatter when you come close or fire a gun;
  - flocks crossing the sky;
  - insects over water by day;
  - fireflies on summer evenings;
  - all of it thinning out in deep woods and gone in Thin places.
- **Sound hooks:** rain, wind, birdsong, insects and thunder. These are audio sources with no clips yet;
  see *Needs*.
- **Cheats:** set the weather, and skip to an hour.

**Out:**
- exposure and wet clothing (wet cloth after rain is in DESIGN, for later);
- snow and seasonal ground colour;
- wildlife you can hunt or that the sim tracks (herds and boar are later hunts);
- puddles and flowing-water sound placement.

## Simulation

- **Content:** `Content/weather.yaml` sets, for each season, how likely each kind of weather is and how
  likely it is to persist.
  - Spring: rain often.
  - Summer: clear, with afternoon storms.
  - Autumn: fog in the mornings.
  - Winter: overcast.
- **State:** `SimState.Weather` holds the current kind, the hour it started, and the next change. Rolls
  use `SimRandom`.
- **Rules:**
  - Each new day rolls the morning weather from the day before (persistence chance per season).
  - There's a chance of one change in the afternoon.
  - A storm never lasts more than half a day.
  - Morning fog burns off by noon unless it rolls to persist.
- **Commands:**
  - `SetWeather` (cheat);
  - `AdvanceHours` (existing day advance) drives the changes.
- **Domain events out:** `WeatherChanged` (from, to, hour).
- **Queries:** `Weather`, and `WeatherAt(hour)` for the forecast line in the HUD ("rain by afternoon"),
  which only shows when someone in the household has the Weather Sense ability. That's a new vocab
  ability; it is skipped if you'd rather not have it.
- **Events:** the trigger takes `weather_any: [rain, storm]`, and the vocab gains a `weather` list. Two or
  three existing events get weather variants: the creek event in a flood, a stranger sheltering from a
  storm.

## Presentation

- **`WeatherView`** reads the sim's weather and blends over about 20 s of game time into:
  - `Atmosphere` multipliers (sun, ambient, fog);
  - the sky's cloud cover;
  - foliage wind strength;
  - a global `_Wetness` value the ground, foliage and greybox shaders use to darken and gloss.
- **Rain:**
  - a particle system parented to the camera rig;
  - streaks, with splashes on the ground near the camera;
  - heavier in a storm;
  - lightning lights the sky and the scene for a frame, with thunder a moment later.
- **`AmbientLife`** spawns around the player from pools and recycles them as you move:
  - **Perched birds:** small greybox birds on tree crowns near the player. Each one flushes and flies off
    when you come within 12 m (or 6 m crouched), when anyone fires within 60 m, or when a deer bolts
    nearby. It doesn't come back while you're close.
  - **Flocks:** every few minutes a loose V or a cloud of birds crosses the sky high up.
  - **Insects:** a shimmer of gnats over water and in sunny clearings by day.
  - **Fireflies:** dusk to midnight in summer and late spring, in meadows and at woods' edges.
  - Density follows the area: full in meadows and woods edges, half in deep woods, none in Thin places.
    Rain and storms send birds and insects under cover.
- All densities, distances and timings are serialized fields for Travis to tune.

## Acceptance tests

- **Sim:**
  - the same seed gives the same weather run;
  - seasons follow their tables over many days (spring wetter than summer, and so on);
  - storms end by the next half-day;
  - weather survives the save;
  - `SetWeather` emits `WeatherChanged`;
  - an event with `weather_any` only fires in that weather;
  - content validation for `weather.yaml` and `weather_any`.
- **PlayMode:**
  - setting rain turns rain on and raises wetness;
  - setting clear clears it;
  - a perched bird flushes when the player walks up, or when a shot is fired;
  - there are no birds in a Thin place;
  - fireflies appear at dusk in summer and not at noon.

## Needs (Travis)

- **Sound:** OK to download CC0 ambience (rain, wind, birdsong, insects, thunder) from a free library, for
  example Freesound CC0 or Sonniss GDC bundles? Until then, the audio sources are in place but silent.
