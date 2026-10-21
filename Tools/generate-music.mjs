// Generates the game's background music with the ElevenLabs Music API into Assets/Audio/Music,
// where Ninja/Apply Audio picks it up. Run with `npm run music` (all tracks) or `npm run music -- BattleMusic`.
// The SDK reads ELEVENLABS_API_KEY, which the npm script loads from .env.local.
import { ElevenLabsClient } from "@elevenlabs/elevenlabs-js";
import { createWriteStream } from "fs";
import { mkdir } from "fs/promises";
import { Readable } from "stream";
import { pipeline } from "stream/promises";

const OUT_DIR = new URL("../Assets/Audio/Music/", import.meta.url);

// Both tracks loop in game, so each asks for an ending that flows back into its opening.
const TRACKS = {
  TitleMusic: {
    lengthMs: 90_000,
    prompt:
      "Calm, atmospheric feudal Japan title theme for a pixel-art ninja game. Solo shakuhachi flute melody over " +
      "gentle koto arpeggios, soft taiko pulses and temple wind ambience. Mysterious and serene, slow tempo, " +
      "instrumental only. Seamless loop: the ending flows straight back into the opening with no fade out.",
  },
  BattleMusic: {
    lengthMs: 120_000,
    prompt:
      "Driving, energetic feudal Japan battle music for a retro arcade ninja game. Pounding taiko drum ensemble, " +
      "fast shamisen riffs, sharp shakuhachi accents and a punchy chiptune bass line. Intense and heroic, about " +
      "150 BPM, steady energy throughout, instrumental only. Seamless loop: the ending flows straight back into " +
      "the opening with no intro build or fade out.",
  },
};

const client = new ElevenLabsClient();
const wanted = process.argv.slice(2);
const names = wanted.length > 0 ? wanted : Object.keys(TRACKS);

await mkdir(OUT_DIR, { recursive: true });
for (const name of names) {
  const track = TRACKS[name];
  if (!track) {
    console.error(`Unknown track "${name}". Known: ${Object.keys(TRACKS).join(", ")}`);
    process.exitCode = 1;
    continue;
  }

  console.log(`Composing ${name} (${track.lengthMs / 1000}s)...`);
  try {
    const audio = await client.music.compose({
      prompt: track.prompt,
      musicLengthMs: track.lengthMs,
      modelId: "music_v2_5",
      forceInstrumental: true,
    });
    const source = typeof audio.pipe === "function" ? audio : Readable.fromWeb(audio);
    const file = new URL(`${name}.mp3`, OUT_DIR);
    await pipeline(source, createWriteStream(file));
    console.log(`Saved ${file.pathname}`);
  } catch (err) {
    console.error(`API error for ${name}:`, err.message ?? err);
    process.exitCode = 1;
  }
}
