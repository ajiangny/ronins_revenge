# Outside Unity

Part of the [architecture map](../ARCHITECTURE.md). Scripts live under `Assets/Scripts/<System>/`; a bare name like
`Ronin.cs` means the file in its system's folder.

- `Tools/generate-music.mjs` — regenerates music via ElevenLabs (`npm run music`, key in gitignored `.env.local`).
- `Tools/generate-sfx.mjs` / `Tools/generate-voice.mjs` — the newer sound effects, all of Benibana's lines in her designed voice, all of Utsusemi's lines in its designed voice, all of Shirogane's in Lily's and all of Kagetsune's in his designed voice (plus the three souls' answers in theirs), all of Oni's in his designed voice, the three souls' `SoulPickup` lines in theirs, and every Poguru line in his designed voice (`POGURU`; Oni's replies in his), and the tutorial's dead soldiers (`SOLDIER`), whose takes are baked through `ffmpeg` with the `GHOST` filter (`FX` maps a voice to its filter; raw takes are kept in the gitignored `Tools/.voice-raw/`, and `npm run voice -- --fx-only` re-bakes them without API calls) (`npm run sfx`, `npm run voice`).
- `README.md` — the assignment write-up (requirements, template fixes, features, balancing).


## Import hooks

- `Editor/PixelArtImporter.cs`: everything under `Assets/Art/Sprites/` imports as crisp pixel art.
- `Editor/WebImportSettings.cs`: portraits, the logo, sound effects and voice lines get web-friendly settings;
  `Ninja/Apply Web Import Settings` reimports them.
