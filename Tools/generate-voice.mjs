// Generates Benibana's mid-fight barks (move callouts, hurt gasps, her stance breaking) with ElevenLabs
// text-to-speech in her Izumi voice, into Assets/Audio/Voice where Ninja/Apply Boss Art picks them up.
// Run with `npm run voice` (all) or `npm run voice -- Benibana_Iaido`. Reads ELEVENLABS_API_KEY from .env.local.
import { ElevenLabsClient } from "@elevenlabs/elevenlabs-js";
import { createWriteStream } from "fs";
import { mkdir } from "fs/promises";
import { Readable } from "stream";
import { pipeline } from "stream/promises";

const OUT_DIR = new URL("../Assets/Audio/Voice/", import.meta.url);
const IZUMI = "cENJycK4Wg62xVikqkaA";

// eleven_v3 reads the bracketed audio tags as delivery directions. Keep the spoken words in sync with
// Benibana.Line in Assets/Benibana.cs, which shows them as subtitles.
const LINES = {
  Benibana_SmokeRings: "[sultry, amused] Breathe it in...",
  Benibana_RosaryLash: "[commanding, sharp] Kneel!",
  Benibana_Iaido: "[intense, drawing her sword] Crimson... Iaido!",
  Benibana_RosaryBloom: "[furious shout] Bloom, my rosary!",
  Benibana_Stagger: "[shocked gasp] What?! My own blade...",
  Benibana_Hurt: "[pained grunt] Ngh!",
  Benibana_Hurt_2: "[pained cry] Hah!",
  Benibana_Hurt_3: "[sharp pained gasp] Agh!",
};

const client = new ElevenLabsClient();
const wanted = process.argv.slice(2);
const names = wanted.length > 0 ? wanted : Object.keys(LINES);

await mkdir(OUT_DIR, { recursive: true });
for (const name of names) {
  const text = LINES[name];
  if (!text) {
    console.error(`Unknown line "${name}". Known: ${Object.keys(LINES).join(", ")}`);
    process.exitCode = 1;
    continue;
  }

  console.log(`Voicing ${name}...`);
  try {
    const audio = await client.textToSpeech.convert(IZUMI, {
      text,
      modelId: "eleven_v3",
      outputFormat: "mp3_44100_128",
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
