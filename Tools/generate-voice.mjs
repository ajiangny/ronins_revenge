// Generates the bosses' voice lines with ElevenLabs text-to-speech: all of Benibana's lines in her designed voice,
// Utsusemi's in its designed voice, Shirogane's in Lily's and Kagetsune's in his designed voice (the story behind each is in Story/<Boss>.md).
// They land in Assets/Audio/Voice, where Ninja/Apply Boss Art
// picks them up. Run with `npm run voice` (all) or `npm run voice -- Utsusemi_Intro`. Reads ELEVENLABS_API_KEY
// from .env.local.
import { ElevenLabsClient } from "@elevenlabs/elevenlabs-js";
import { createWriteStream } from "fs";
import { mkdir } from "fs/promises";
import { Readable } from "stream";
import { pipeline } from "stream/promises";

const OUT_DIR = new URL("../Assets/Audio/Voice/", import.meta.url);
// Designed in ElevenLabs for Benibana: a low, smoky, unaccented alto with a stage performer's diction, the court
// dancer used to an audience, theatrical and mocking with cold grief underneath.
const BENIBANA = "piUPVF9NiOg0l2da9TTF";
// Designed in ElevenLabs for Utsusemi: a deep, smooth machine villain with a dry, calm menace. Unity adds the
// metallic ring on top (GameManager's voice filters).
const UTSUSEMI = "nCaDofVjs6twTSKyrLJG";
// Shirogane: ElevenLabs' Lily, a low, velvety actress's voice, read calm and mournful; Unity adds the ghostly
// echo and drift (GameManager's spirit retune of the voice filters).
const SHIROGANE = "pFZP5JQG7iQjIQuC4Bku";
// Designed in ElevenLabs for Kagetsune: an extremely deep, slow, resonant bass, imperious and contemptuous, as if from
// inside an iron helm. Unity layers the choir of souls under it (GameManager's choir retune of the voice filters).
const KAGETSUNE = "icDlqXtTOaetg6DNGlyW";

// [voice, text]. eleven_v3 reads the bracketed audio tags as delivery directions. Keep the spoken words in sync
// with Benibana.Line / Utsusemi.Line / Shirogane.Line / Kagetsune.Line, which show them as subtitles.
const LINES = {
  Benibana_Intro: [BENIBANA, "[low, bitter, amused] We both led our flocks to his altar, Oni... [cold] and only you walked away. [sultry, mocking] The heavens have forsaken you. Let me paint your final moments in crimson."],
  Benibana_Phase: [BENIBANA, "[quiet, shaking with fury] Every step I ever danced was his... [fierce, drawing her sword] This one is mine."],
  Benibana_Defeat: [BENIBANA, "[weak, wry, fading] Crimson... suits you... [pleading, breathless] Find my bell, Oni... silence it for them..."],
  // Each move callout has three takes, picked at random; the texts match Benibana.Line's subtitles take for take.
  Benibana_SmokeRings: [BENIBANA, "[sultry, amused] Breathe it in..."],
  Benibana_SmokeRings_2: [BENIBANA, "[sultry whisper] Inhale..."],
  Benibana_SmokeRings_3: [BENIBANA, "[amused] Lose yourself."],
  Benibana_RosaryLash: [BENIBANA, "[commanding, sharp] Kneel!"],
  Benibana_RosaryLash_2: [BENIBANA, "[contemptuous] Bow your head."],
  Benibana_RosaryLash_3: [BENIBANA, "[sharp] Down!"],
  // Quick and clipped, no dramatic pause: the move is over in about a second.
  Benibana_Iaido: [BENIBANA, "[sharp, fast, drawing her sword] Crimson Iaido!"],
  Benibana_Iaido_2: [BENIBANA, "[cold, low] Iaido."],
  Benibana_Iaido_3: [BENIBANA, "[mocking] Too slow."],
  Benibana_RosaryBloom: [BENIBANA, "[furious shout] Bloom, my rosary!"],
  Benibana_RosaryBloom_2: [BENIBANA, "[furious] Scatter!"],
  Benibana_RosaryBloom_3: [BENIBANA, "[fierce] Pray!"],
  // Her stance breaks when a parry turns her own attack back on her; two takes, each a grunt as the blow lands, then a short, clipped
  // line from someone who has lost to this parry before (every fight is a rematch).
  Benibana_Stagger: [BENIBANA, "[sharp staggered grunt, knocked back] Ngh! [furious, through gritted teeth] Dirty trick!"],
  Benibana_Stagger_2: [BENIBANA, "[sharp staggered grunt, knocked back] Agh! [quick, furious, spitting it out] You dog!"],
  Benibana_Hurt: [BENIBANA, "[pained grunt] Ngh!"],
  Benibana_Hurt_2: [BENIBANA, "[pained cry] Hah!"],
  Benibana_Hurt_3: [BENIBANA, "[sharp pained gasp] Agh!"],
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
  Shirogane_KuonEnraged: [SHIROGANE, "[weak, kneeling, breathless] Kuon... [fierce, urgent] don't hold back."],
  Shirogane_Hurt: [SHIROGANE, "[short pained breath] Hh!"],
  Shirogane_Hurt_2: [SHIROGANE, "[sharp wince] Ah!"],
  Shirogane_Hurt_3: [SHIROGANE, "[quiet pained exhale] Nn..."],
  // The ending: Oni cuts the chain binding her soul to the fallen Emperor's hammer, as she once cut his tether.
  Shirogane_Freed: [SHIROGANE, "[soft, wondering, almost laughing through tears] You cut it... [tender] the way I cut yours."],
  Shirogane_Farewell: [SHIROGANE, "[warm, at peace] The oath is kept. [gentle, smiling] Play for me, Oni... one last time."],
  // The Emperor. He never hurries and never shouts until his phase break.
  Kagetsune_Intro: [KAGETSUNE, "[very slow, deep, contemptuous] Every bell upon me is a soul that knelt, Oni. [pause] One still hangs silent... [cold] yours."],
  Kagetsune_Phase: [KAGETSUNE, "[roaring, furious] Kneel! KNEEL! [ragged, desperate] You are MINE, every one of you..."],
  Kagetsune_Defeat: [KAGETSUNE, "[faltering, afraid, fading] The bells... why do they not ring...?"],
  Kagetsune_Stagger: [KAGETSUNE, "[shocked, enraged] You dare ring MY bell?"],
  Kagetsune_KilledOni: [KAGETSUNE, "[slow, satisfied, contemptuous] Back to your hill, little demon. I will be here. [pause] I am always here."],
  Kagetsune_CofferSlam: [KAGETSUNE, "[deep, commanding] Bow."],
  Kagetsune_Toll: [KAGETSUNE, "[slow, imperious] Answer me."],
  Kagetsune_Ofuda: [KAGETSUNE, "[cold, formal] By my decree."],
  Kagetsune_Wail: [KAGETSUNE, "[slow, cruel] Hear them, little demon."],
  Kagetsune_LastToll: [KAGETSUNE, "[slow, grave] Hear the last toll."],
  Kagetsune_Swarm: [KAGETSUNE, "[contemptuous] Go. Fetch him."],
  Kagetsune_Chains: [KAGETSUNE, "[snarling] Chains do not hold me."],
  Kagetsune_Coffin: [KAGETSUNE, "[roaring] Into the coffin!"],
  Kagetsune_ThousandBells: [KAGETSUNE, "[booming, exultant] A thousand bells, Oni! Ring!"],
  Kagetsune_SummonBenibana: [KAGETSUNE, "[slow, mocking] Dance for me, Benibana."],
  Kagetsune_SummonUtsusemi: [KAGETSUNE, "[cold, bored] Strike the hour, clock."],
  Kagetsune_SummonShirogane: [KAGETSUNE, "[commanding, a trace of unease] Rise, Shirogane. Kneel, and strike."],
  Kagetsune_Hurt: [KAGETSUNE, "[deep, irritated] Insolence."],
  Kagetsune_Hurt_2: [KAGETSUNE, "[slow, contemptuous] A scratch, little demon."],
  Kagetsune_Hurt_3: [KAGETSUNE, "[low grunt] Hm."],
  // The souls he drags out of the coffin, answering in their own voices.
  Benibana_Echo: [BENIBANA, "[hollow, sorrowful, distant] Forgive me, Oni... he has my bell."],
  Utsusemi_Echo: [UTSUSEMI, "[slow, grinding, straining] The hour... strikes... [urgent whisper] Run... boy."],
  Shirogane_Echo: [SHIROGANE, "[strained, urgent, sorrowful] Move, Oni. [anguished] I cannot hold my blades."],

  // The reactive dialogue's lines (Dialogue.cs): ids are conversation ids, and the words must match its text. Run
  // Ninja/Apply Voice Lines after generating them. Utsusemi_Intro_Counted fills in a death count, so it stays a
  // subtitle.
  Benibana_Intro_Three: [BENIBANA, "[cold, counting, bitter] Three times I've killed you. Three bells he swore to silence. [quiet] Not one has stopped ringing."],
  Benibana_Intro_Bell: [BENIBANA, "[low, confiding, almost tender] The third bell from the left on his belt. The cracked one. [quiet] That one is mine."],
  Benibana_Intro_Back: [BENIBANA, "[amused, mocking] Back so soon? [bitter] He promised me a bell for that one. I'm still listening."],
  Benibana_Intro_Fire: [BENIBANA, "[amused, scornful] Fire? You'd burn my smoke? [sultry] Cute."],
  Benibana_Intro_Dance: [BENIBANA, "[sultry, inviting] Dance with me, demon."],
  Benibana_Killed: [BENIBANA, "[hushed, listening] Listen... Did a bell stop? [bitter, quiet] ...No. Of course not."],
  Benibana_Killed_Curtain: [BENIBANA, "[cold, final] Curtain."],
  Utsusemi_Intro_Tea: [UTSUSEMI, "[slow, ticking] Tick... tock... tea... [confused, softer, almost human] There was tea, once. Why do I... remember tea?"],
  Utsusemi_Intro_WindDown: [UTSUSEMI, "[strained, then a gentle old man's whisper] Boy. Wind me down. I cannot do it... from in here."],
  Utsusemi_Intro_Tick: [UTSUSEMI, "[slow, cold] Tick... tock."],
  Utsusemi_Killed: [UTSUSEMI, "[cold, deliberate] One. I will remember the hour."],
  Utsusemi_Killed_Counted: [UTSUSEMI, "[cold, clipped] Counted."],
  Shirogane_Intro_Bell: [SHIROGANE, "[calm, proud, sorrowful] She told you which bell is hers. Mine has never rung. I would not kneel."],
  Shirogane_Intro_Oath: [SHIROGANE, "[quiet, steady, mournful] We swore to end him together, before the coronation. I kept the oath. [firm] Now you keep it."],
  Shirogane_Intro_Hesitated: [SHIROGANE, "[soft, disappointed] You hesitated. You never used to."],
  Shirogane_Intro_Again2: [SHIROGANE, "[calm, resolute] Again, then."],
  Shirogane_Killed: [SHIROGANE, "[gentle, sad] Not yet, then. I'll wait."],
  Shirogane_Killed_Rise: [SHIROGANE, "[quiet, firm] Rise."],
  Kagetsune_Intro_Broken: [KAGETSUNE, "[slow, contemptuous] You broke me once. [booming] I have a thousand more of me."],
  Kagetsune_Intro_Silent: [KAGETSUNE, "[irritated, contemptuous] She never rang. Do you know how that grates, little demon?"],
  Kagetsune_Intro_Kneel: [KAGETSUNE, "[deep, commanding] Kneel."],
  Kagetsune_Killed_Duo: [KAGETSUNE, "[mocking, slow] Two stolen souls, holding hands. How touching. [cold] They'll ring again."],
  Kagetsune_Killed_Ring: [KAGETSUNE, "[deep, satisfied] Ring."],
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
