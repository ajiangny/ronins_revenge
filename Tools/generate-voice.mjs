// Generates the bosses' voice lines with ElevenLabs text-to-speech: all of Benibana's lines in her Izumi voice,
// Utsusemi's in its designed voice and Shirogane's in Lily's (the story behind each is in Story/<Boss>.md).
// They land in Assets/Audio/Voice, where Ninja/Apply Boss Art
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
// Shirogane: ElevenLabs' Lily, a low, velvety actress's voice, read calm and mournful; Unity adds the ghostly
// echo and drift (GameManager's spirit retune of the voice filters).
const SHIROGANE = "pFZP5JQG7iQjIQuC4Bku";

// [voice, text]. eleven_v3 reads the bracketed audio tags as delivery directions. Keep the spoken words in sync
// with Benibana.Line / Utsusemi.Line / Shirogane.Line, which show them as subtitles.
const LINES = {
  Benibana_Intro: [IZUMI, "[low, bitter, amused] We both led our flocks to his altar, Oni... [cold] and only you walked away. [sultry, mocking] The heavens have forsaken you. Let me paint your final moments in crimson."],
  Benibana_Phase: [IZUMI, "[quiet, shaking with fury] Every step I ever danced was his... [fierce, drawing her sword] This one is mine."],
  Benibana_Defeat: [IZUMI, "[weak, wry, fading] Crimson... suits you... [pleading, breathless] Find my bell, Oni... silence it for them..."],
  // Each move callout has three takes, picked at random; the texts match Benibana.Line's subtitles take for take.
  Benibana_SmokeRings: [IZUMI, "[sultry, amused] Breathe it in..."],
  Benibana_SmokeRings_2: [IZUMI, "[sultry whisper] Inhale..."],
  Benibana_SmokeRings_3: [IZUMI, "[amused] Lose yourself."],
  Benibana_RosaryLash: [IZUMI, "[commanding, sharp] Kneel!"],
  Benibana_RosaryLash_2: [IZUMI, "[contemptuous] Bow your head."],
  Benibana_RosaryLash_3: [IZUMI, "[sharp] Down!"],
  // Quick and clipped, no dramatic pause: the move is over in about a second.
  Benibana_Iaido: [IZUMI, "[sharp, fast, drawing her sword] Crimson Iaido!"],
  Benibana_Iaido_2: [IZUMI, "[cold, low] Iaido."],
  Benibana_Iaido_3: [IZUMI, "[mocking] Too slow."],
  Benibana_RosaryBloom: [IZUMI, "[furious shout] Bloom, my rosary!"],
  Benibana_RosaryBloom_2: [IZUMI, "[furious] Scatter!"],
  Benibana_RosaryBloom_3: [IZUMI, "[fierce] Pray!"],
  Benibana_Stagger: [IZUMI, "[shocked gasp] What?! My own blade..."],
  Benibana_Hurt: [IZUMI, "[pained grunt] Ngh!"],
  Benibana_Hurt_2: [IZUMI, "[pained cry] Hah!"],
  Benibana_Hurt_3: [IZUMI, "[sharp pained gasp] Agh!"],
  Utsusemi_Intro: [UTSUSEMI, "[slow, cold, deliberate] Tick... tock... The monk who wore this hat once gave you shelter. Now I walk every road he walked, looking for you. [quietly menacing] Yours... ends here."],
  Utsusemi_Phase: [UTSUSEMI, "[strained, then rising menace] Mainspring... unwound. [cold fury] I will not... stop."],
  Utsusemi_Defeat: [UTSUSEMI, "[slowing down, fading away] The bell... has... stopped... [suddenly warm and human, a gentle old man's whisper] Run... boy..."],
  Utsusemi_Stagger: [UTSUSEMI, "[startled, faltering] Gears... seized..."],
  Utsusemi_Kasa: [UTSUSEMI, "[cold, clipped] Kasa."],
  Utsusemi_HourStrikes: [UTSUSEMI, "[cold, deliberate, counting] Remember the hours... [quietly menacing] one... by one."],
  Utsusemi_Hurt: [UTSUSEMI, "[short mechanical grunt] Hngh."],
  Utsusemi_Hurt_2: [UTSUSEMI, "[sharp, irritated] Kh!"],
  Utsusemi_Hurt_3: [UTSUSEMI, "[low pained exhale] Hah..."],
  Shirogane_Intro: [SHIROGANE, "[calm, low, mournful] I remember your blade. [quiet, searching] Do you remember mine?"],
  Shirogane_Phase: [SHIROGANE, "[quiet, sad] You still fight as you did before... [resolute, rising] Then let us remind you what you've forgotten."],
  Shirogane_Defeat: [SHIROGANE, "[weak, tender, breathless] Even after all this time... you still wield the blade I remember."],
  Shirogane_DefeatFinal: [SHIROGANE, "[gentle, fading, at peace] Go. Finish what we started."],
  Shirogane_Stagger: [SHIROGANE, "[startled gasp, faltering] My stance..."],
  Shirogane_Crescent: [SHIROGANE, "[calm, cold] Crescent Moon."],
  Shirogane_BladeRain: [SHIROGANE, "[soft command] Blades... fall."],
  Shirogane_Eclipse: [SHIROGANE, "[slow, intense, building] Eclipse... [fierce] Fang."],
  Shirogane_Hurt: [SHIROGANE, "[short pained breath] Hh!"],
  Shirogane_Hurt_2: [SHIROGANE, "[sharp wince] Ah!"],
  Shirogane_Hurt_3: [SHIROGANE, "[quiet pained exhale] Nn..."],
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
