# 0025 — Weather in the simulation, life in presentation

Status: accepted (spec approved 2026-11-17: docs/specs/life-and-weather.md).

## Decisions

- **Weather is simulation state** (`SimState.Weather`), because it must persist and events read it. The sim
  counts days, not hours, so **each day's weather is planned as spells when the day begins**:
  - the morning, often yesterday's weather carrying on (a storm never does);
  - morning fog lifting before noon;
  - maybe one change in the afternoon;
  - a storm always blowing over within about seven hours.

  Presentation asks `WeatherAt(hour)` with its clock's hour. Mid-day changes raise no domain event, since
  the sim never ticks hours; `WeatherChanged` fires at dawn and from the `SetWeather` cheat.
- **Its own random stream** (seed and day hashed), so adding weather reshuffled nothing else in any seed.
- **The climate is content:** `Content/weather.yaml`, with built-in defaults when it's absent.
- **Weather kinds are an enum** (like `time_of_day`), not a vocab list.
- **Events** can ask `when.weather_any`. `EventContext.Hour` carries the clock's hour; without it, a typical
  hour for the time of day is used.
- **Weather Sense comes from a Mark:** stiff joints (`age_stiff`) now grant `weather_sense`, so the elders
  feel rain coming and the HUD shows the forecast. The "aches before rain" folk truth gives old age a use.
- **`WeatherView`** (presentation) blends into the weather over about 20 s:
  - the `Atmosphere` multipliers and the sky's clouds, on a runtime copy so play never edits the asset;
  - global `_HL_WindBoost` and `_HL_Wetness` for the foliage shader;
  - a darker, glossier ground material copy;
  - rain and splash particles around the camera;
  - lightning and thunder.
- **`AmbientLife`** (presentation only, nothing saved):
  - perched and foraging birds that flush (on approach, a gunshot or a bolting deer);
  - flocks overhead;
  - gnats over creeks by day;
  - fireflies on summer evenings.

  It thins in deep woods and bad weather, and is **silent in Thin places**. The deep hollow is Thin level 1,
  so it is silent too.
- **Sound:** audio sources are in place without clips until Travis OKs CC0 downloads.
- **The Particle System module** is now enabled in `Packages/manifest.json`; it was off.
