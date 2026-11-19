# Art reference

Drop reference images here for Claude (and anyone else) to work from. This folder sits outside `Assets/`,
so Unity does not import it.

**New files here are git-ignored.** The repository is public and reference images often belong to someone
else. The images Travis generated himself (the people turnarounds, places and style images from 2026-11-18)
were added on purpose with `git add -f`. To publish another image you made yourself, add it the same way. Never
force-add someone else's work.

## Folders

| Folder | What goes in it |
| --- | --- |
| `style/` | The overall look: palette, lighting, mood, how stylised or realistic, paintings or film stills you like |
| `people/` | Faces, builds, ages, clothing layers, how people of this world carry themselves |
| `marks/` | Scars, wounds (fresh, bandaged, infected), burns, missing fingers, eyepatches, slings, brands, aging; the exposures (pale eyes, glass skin) |
| `places/` | The overgrown Southeast: kudzu towns, woods, creeks, ridges, ruins, homesteads |
| `gear/` | Hats, coats, boots, knives, revolvers, lever rifles, worn and repaired things |
| `not-this/` | Looks to avoid. Just as useful as the rest |

Images (PNG, JPG, WebP) and PDFs all work. Claude can look at them directly.

## Notes help a lot

A one-line note per image or folder tells Claude what you like about it, which matters more than the image
itself. Put it in a `notes.md` in the folder, for example:

```
people/old_trapper.jpg  - the weathered skin and the way the coat is patched, not the beard
marks/burn_hand.png     - this level of realism is the ceiling; nothing gorier
style/creek_dusk.jpg    - this light, for evenings
```

The visual keys every mark and item needs art for are listed under `visuals:` in `Content/vocab.yaml`.
Briefs and prompts for each are in `docs/art/reference-prompts.md`.
