// Generates the game's background music with the ElevenLabs Music API into Assets/Audio/Music,
// where Ninja/Apply Audio picks it up. Run with `npm run music` (all tracks) or `npm run music -- BattleMusic`.
// Each stage has its rounds music (Stage_<Boss>) and its boss fight (Boss_<Boss>), written from Story/: see the
// character files for why each one sounds the way it does. BattleMusic plays wherever a stage has none.
// The SDK reads ELEVENLABS_API_KEY, which the npm script loads from .env.local.
import { ElevenLabsClient } from "@elevenlabs/elevenlabs-js";
import { createWriteStream } from "fs";
import { mkdir } from "fs/promises";
import { Readable } from "stream";
import { pipeline } from "stream/promises";

const OUT_DIR = new URL("../Assets/Audio/Music/", import.meta.url);

// Every track loops in game, so each asks for an ending that flows back into its opening.
const TRACKS = {
  TitleMusic: {
    lengthMs: 90_000,
    // Peaceful and nostalgic, in the spirit of classic JRPG title themes and cozy MMO town music, led by the
    // flute the ronin plays on the title screen.
    prompt:
      "Peaceful, nostalgic title theme for a fantasy samurai adventure game, in the style of a classic 1990s JRPG " +
      "prelude and a cozy 2D MMORPG village theme. A warm, breathy bamboo flute plays a gentle, singable lead " +
      "melody over flowing harp arpeggios, soft koto plucks, a warm string pad and light wind chimes. No drums, " +
      "about 72 BPM, tender, hopeful and a little wistful, like a quiet evening by a mountain shrine. " +
      "Instrumental only. Seamless loop: the ending flows straight back into the opening with no fade out.",
  },
  BattleMusic: {
    lengthMs: 120_000,
    prompt:
      "Driving, energetic feudal Japan battle music for a retro arcade ninja game. Pounding taiko drum ensemble, " +
      "fast shamisen riffs, sharp shakuhachi accents and a punchy chiptune bass line. Intense and heroic, about " +
      "150 BPM, steady energy throughout, instrumental only. Seamless loop: the ending flows straight back into " +
      "the opening with no intro build or fade out.",
  },
  // The Bamboo Road.
  Stage_Benibana: {
    lengthMs: 120_000,
    prompt:
      "Night march through a moonlit bamboo forest on the road to the capital, for a retro arcade ninja game's first " +
      "stage. Driving taiko and shime-daiko groove, fast plucked shamisen riffs, a breathy shakuhachi melody that " +
      "cuts through like wind in the bamboo stalks, hollow bamboo clacks and a punchy chiptune bass. Determined, lean " +
      "and adventurous, a lone ronin setting out to hunt his betrayer, about 145 BPM, steady energy throughout. " +
      "Instrumental only. Seamless loop: the ending flows straight back into the opening with no intro build or fade " +
      "out.",
  },
  // Benibana, the Crimson Smoke: a court sword-dancer and assassin under a blood moon, a hidden Christian whose
  // people ring the Emperor's bells.
  Boss_Benibana: {
    lengthMs: 120_000,
    prompt:
      "Seductive, theatrical boss duel against a crimson court sword-dancer under a blood-red moon. A swaying " +
      "sword-dance rhythm on shamisen and koto, sharp hyoshigi clapper accents like a kabuki stage, sultry low " +
      "shakuhachi bends, a heavy taiko pulse, and underneath it all a mournful, hushed sacred chant melody on a small " +
      "reed organ with distant church-like bells, a secret prayer she can't stop hearing. Elegant, dangerous and " +
      "tragic, building to furious, about 150 BPM. Instrumental only. Seamless loop: the ending flows straight back " +
      "into the opening with no intro build or fade out.",
  },
  // The Pilgrim's Pass.
  Stage_Utsusemi: {
    lengthMs: 120_000,
    prompt:
      "A cold, windswept mountain pilgrim road at night, bigger and angrier armies than before. Relentless taiko " +
      "ensemble at a faster pace, a rapid mokugyo wooden temple-block pulse, biwa lute strums, a shinobue flute " +
      "melody over howling wind, low Buddhist bonsho temple bell hits on the downbeat and a gritty chiptune bass. " +
      "Urgent and climbing, about 158 BPM, steady energy throughout. Instrumental only. Seamless loop: the ending " +
      "flows straight back into the opening with no intro build or fade out.",
  },
  // Utsusemi, the Hollow Pilgrim: a kind monk's soul wound into a clockwork karakuri that winds the clock of the
  // stopped night.
  Boss_Utsusemi: {
    lengthMs: 120_000,
    prompt:
      "Menacing clockwork boss battle against a karakuri automaton wearing a dead monk's hat, under a cold steel " +
      "moon. A strict, mechanical ticking percussion locked to the tempo, gear clicks and wound-spring music-box koto " +
      "ostinatos, a low droning Buddhist sutra chant pad, metallic struck bowls, deep taiko and a distorted " +
      "industrial bass. Precise, eerie and relentless like a clock that cannot stop, with a brief sorrowful " +
      "shakuhachi phrase of the kind monk still trapped inside, about 128 BPM in perfectly strict time. Instrumental " +
      "only. Seamless loop: the ending flows straight back into the opening with no intro build or fade out.",
  },
  // The Frozen Shrine, where the dead march too.
  Stage_Shirogane: {
    lengthMs: 120_000,
    prompt:
      "A snowbound shrine at night where an army of the dead marches. Eerie and cold: icy glass harmonics and " +
      "celesta, a ghostly wordless female choir drifting in the distance, a haunting nohkan flute, slow heavy taiko " +
      "like marching corpses that quickens into a driving beat, plucked koto like falling ice and a deep chiptune " +
      "bass. Spectral, chilling and urgent, about 140 BPM, steady energy throughout. Instrumental only. Seamless " +
      "loop: the ending flows straight back into the opening with no intro build or fade out.",
  },
  // Shirogane, the Forsaken Blade: Oni's sworn partner, now a ghost with twin blue-flamed katanas and her shrine's
  // spirit wolf, forced to fight him and wanting him to win.
  Boss_Shirogane: {
    lengthMs: 120_000,
    prompt:
      "Heartbreaking, heroic duel against a ghost who was the hero's sworn partner, under a pale blue spirit moon. " +
      "Soaring, urgent strings and two interlocking koto lines like crossing twin blades, a lonely bamboo flute " +
      "playing a tender remembered melody over the fury, powerful taiko, shimmering icy bells and a distant wolf-like " +
      "horn call. Fierce and beautiful at once, grief and loyalty, a test rather than an execution, about 140 BPM. " +
      "Instrumental only. Seamless loop: the ending flows straight back into the opening with no intro build or fade " +
      "out.",
  },
  // The Mountain Shrine: the stair up to the hall of the Night of a Thousand Bells, with Bellringers rousing the
  // army.
  Stage_Kagetsune: {
    lengthMs: 120_000,
    prompt:
      "The final ascent up a shrine stairway under a starless sky toward a tyrant's hall. Ominous and massive: " +
      "tolling bronze temple bells on every bar, thunderous o-daiko war drums, a low chanting male choir, blaring " +
      "horagai conch calls, driving shamisen and a heavy distorted chiptune bass. Dread and defiance, the last march, " +
      "about 150 BPM, steady energy throughout. Instrumental only. Seamless loop: the ending flows straight back into " +
      "the opening with no intro build or fade out.",
  },
  // Kagetsune, the Thousand-Soul Throne: the deathless Emperor in an armor of a thousand sealed souls, each ringing
  // a bell, wreathed in teal soul-fire.
  Boss_Kagetsune: {
    lengthMs: 150_000,
    prompt:
      "Epic final boss battle against a colossal deathless emperor whose armor holds a thousand imprisoned souls. A " +
      "vast, dark choir of a thousand voices chanting like Buddhist shomyo, enormous bronze bells clashing and " +
      "tolling, a full taiko orchestra, a thundering low brass and string orchestra, a fierce biwa and shamisen line " +
      "and a heroic shakuhachi melody of the lone ronin rising against it all. Overwhelming, apocalyptic and defiant, " +
      "about 155 BPM, relentless energy. Instrumental only. Seamless loop: the ending flows straight back into the " +
      "opening with no intro build or fade out.",
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
