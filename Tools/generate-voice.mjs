// Generates the bosses' voice lines with ElevenLabs text-to-speech: Benibana's mid-fight barks in her Izumi voice,
// and all of Utsusemi's lines in its designed voice. They land in Assets/Audio/Voice, where Ninja/Apply Boss Art
// picks them up. Run with `npm run voice` (all) or `npm run voice -- Utsusemi_Intro`. Reads ELEVENLABS_API_KEY
// from .env.local.
import { ElevenLabsClient } from "@elevenlabs/elevenlabs-js";
import { createWriteStream } from "fs";
import { mkdir } from "fs/promises";
import { Readable } from "stream";
import { pipeline } from "stream/promises";

const OUT_DIR = new URL("../Assets/Audio/Voice/", import.meta.url);
const IZUMI = "cENJycK4Wg62xVikqkaA";
// Designed in ElevenLabs for Utsusemi: a deep, smooth machine villain with a dry, calm menace. Unity adds the
// metallic ring on top (GameManager's voice filters).
const UTSUSEMI = "nCaDofVjs6twTSKyrLJG";

// [voice, text]. eleven_v3 reads the bracketed audio tags as delivery directions. Keep the spoken words in sync
// with Benibana.Line / Utsusemi.Line, which show them as subtitles.
const LINES = {
  Benibana_SmokeRings: [IZUMI, "[sultry, amused] Breathe it in..."],
  Benibana_RosaryLash: [IZUMI, "[commanding, sharp] Kneel!"],
  Benibana_Iaido: [IZUMI, "[intense, drawing her sword] Crimson... Iaido!"],
  Benibana_RosaryBloom: [IZUMI, "[furious shout] Bloom, my rosary!"],
  Benibana_Stagger: [IZUMI, "[shocked gasp] What?! My own blade..."],
  Benibana_Hurt: [IZUMI, "[pained grunt] Ngh!"],
  Benibana_Hurt_2: [IZUMI, "[pained cry] Hah!"],
  Benibana_Hurt_3: [IZUMI, "[sharp pained gasp] Agh!"],
  Utsusemi_Intro: [UTSUSEMI, "[slow, cold, deliberate] Tick... tock... The pilgrim who wore this hat walked a thousand roads. I have walked every one of them again. [quietly menacing] Yours... ends here."],
  Utsusemi_Phase: [UTSUSEMI, "[strained, then rising menace] Mainspring... unwound. [cold fury] I will not... stop."],
  Utsusemi_Defeat: [UTSUSEMI, "[slowing down, fading away] The bell... has... stopped..."],
  Utsusemi_Stagger: [UTSUSEMI, "[startled, faltering] Gears... seized..."],
  Utsusemi_Kasa: [UTSUSEMI, "[cold, clipped] Kasa."],
  Utsusemi_HourStrikes: [UTSUSEMI, "[cold, deliberate, counting] Remember the hours... [quietly menacing] one... by one."],
  Utsusemi_Hurt: [UTSUSEMI, "[short mechanical grunt] Hngh."],
  Utsusemi_Hurt_2: [UTSUSEMI, "[sharp, irritated] Kh!"],
  Utsusemi_Hurt_3: [UTSUSEMI, "[low pained exhale] Hah..."],
};

const client = new ElevenLabsClient();
const wanted = process.argv.slice(2);
const names = wanted.length > 0 ? wanted : Object.keys(LINES);

await mkdir(OUT_DIR, { recursive: true });
for (const name of names) {
  const line = LINES[name];
  if (!line) {
    console.error(`Unknown line "${name}". Known: ${Object.keys(LINES).join(", ")}`);
    process.exitCode = 1;
    continue;
  }

  console.log(`Voicing ${name}...`);
  try {
    const [voice, text] = line;
    const audio = await client.textToSpeech.convert(voice, {
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
