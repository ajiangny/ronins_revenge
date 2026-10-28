// Generates the voice lines with ElevenLabs text-to-speech: all of Benibana's lines in her designed voice,
// Utsusemi's in its designed voice, Shirogane's in Lily's, Kagetsune's in his designed voice, Oni's in his, Poguru's
// in his, the tutorial's soldiers' in theirs and the three souls' in theirs (the story behind each is in
// Story/<Name>.md).
// They land in Assets/Audio/Voice, where Ninja/Apply Boss Art
// picks them up. Run with `npm run voice` (all) or `npm run voice -- Utsusemi_Intro`. Reads ELEVENLABS_API_KEY
// from .env.local. `npm run voice -- --fx-only [names]` re-bakes the FX voices' clips (the dead soldiers) from their
// kept raw takes, or from the clip itself the first time, without calling the API. Baking needs ffmpeg on the PATH.
import { ElevenLabsClient } from "@elevenlabs/elevenlabs-js";
import { execFileSync } from "child_process";
import { createWriteStream, existsSync } from "fs";
import { copyFile, mkdir } from "fs/promises";
import { Readable } from "stream";
import { pipeline } from "stream/promises";

const OUT_DIR = new URL("../Assets/Audio/Voice/", import.meta.url);
// The unbaked takes of FX voices, kept (gitignored) so `--fx-only` can re-bake them without new API calls.
const RAW_DIR = new URL("./.voice-raw/", import.meta.url);
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
// Designed in ElevenLabs for Oni: a low, quiet, tired baritone in his late twenties, slightly hoarse, slow and sparing,
// weary and haunted, never theatrical. He plays through no voice filters.
const ONI = "XySBombsrAu9uFxNYdaV";
// Designed in ElevenLabs for the three souls (Story/Souls.md): Akagane, a Japanese samurai captain in his forties with
// a gravelly, angry, clipped bark; Mizore, a dry, unhurried physician of about seventy with a slight rasp; Hibiki, a
// bright, quick, breathless seventeen-year-old drummer. Unity gives all three the same faint coffin echo.
const AKAGANE = "GpSqIE5EUQVhDQQOFpqm";
const MIZORE = "6hb6jR2nJV34vAkWq2Gv";
const HIBIKI = "2R377yXvV0jHG7sPjbOY";
// Designed in ElevenLabs for Poguru (Story/Poguru.md): a tiny cartoon baby-dragon spirit, young, boyish, high and bright
// but warm (not shrill), fast and breathless, giggly, with comedic gasps and whines. Unity plays him through a light
// chorus (GameManager.poguruVoice).
const POGURU = "vHGbxklwUU4QPMN7Wam1";
// Designed in ElevenLabs for the tutorial's soldiers: a gruff, sneering middle-aged foot soldier of Kagetsune's army,
// rough and swaggering with a coward underneath, a period-drama villain grunt. All of them share it. They are dead
// (Story/Army.md), so their takes are read hollow and rasping and baked through GHOST below.
const SOLDIER = "r3povynJfMX3y8tUT4Jg";

// The dead army's voice, baked into the mp3 with ffmpeg so it sounds the same in WebGL builds, where Unity's audio
// filters don't run: the take pitched down ~12% at the same speed, doubled by a slow chorus with a tomb echo, and a
// faint whisper of it pitched up and high-passed underneath. The pad leaves room for the echo's tail.
const GHOST =
  "[0:a]apad=pad_dur=0.7,asetrate=44100*0.88,aresample=44100,atempo=1.13636,asplit=2[v][w];" +
  "[v]chorus=0.6:0.9:55|75:0.45|0.35:0.3|0.4:0.6|0.9,aecho=0.8:0.55:120|280:0.32|0.18[body];" +
  "[w]asetrate=44100*1.12,aresample=44100,atempo=0.89286,highpass=f=2200,adelay=45|45,volume=0.5[hiss];" +
  "[body][hiss]amix=inputs=2:weights=1 0.35:normalize=0,highpass=f=90,lowpass=f=7000,volume=2.5,alimiter=limit=0.9";
// Hibiki's takes swing from shouts to near-whispers, so hers are levelled: a gentle compressor evens out each
// line, then loudnorm brings it to the other souls' loudness.
const LEVEL =
  "[0:a]highpass=f=80,acompressor=threshold=-24dB:ratio=3:attack=5:release=150:makeup=2," +
  "loudnorm=I=-16:TP=-1.5:LRA=7,aresample=44100";
// Which voices are baked, and through what. Every other voice is saved as it arrives.
const FX = { [SOLDIER]: GHOST, [HIBIKI]: LEVEL };

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

  // Oni. Ids follow Conversation.VoiceId: a line's conversation id, with _<n> for the nth line of it, so his answer
  // after a boss's own line is <Boss>_<Line>_2, and in the souls' first meetings his line takes its place in the
  // exchange. Words match Dialogue.cs.
  Oni_Hub_First: [ONI, "[waking, quiet, confused] ...The hill. [softly] Her flute. [troubled] Why am I here?"],
  Oni_Hub_Smoke: [ONI, "[low, uneasy] That smoke. I've breathed it before."],
  Oni_Hub_Hat: [ONI, "[slow, remembering] That hat... [quiet] I've drunk tea under that hat."],
  Oni_Hub_Together: [ONI, "[quiet, aching] Together. We were meant to do it together."],
  Oni_Hub_Ringing: [ONI, "[tired, grim, quiet] He's still ringing."],
  Oni_NotYet: [ONI, "[strained, getting up] ...Not yet."],
  Benibana_Intro_2: [ONI, "[low, steady] I didn't walk away. [quiet] Someone cut me loose."],
  Benibana_Phase_2: [ONI, "[quiet, steady] Then dance it."],
  Benibana_Defeat_2: [ONI, "[soft, a promise] I'll find it."],
  Utsusemi_Intro_2: [ONI, "[quiet, uncertain] ...That hat."],
  Utsusemi_Phase_2: [ONI, "[low, resolute] Then I'll stop you."],
  Utsusemi_Defeat_2: [ONI, "[stunned, breaking] ...You. [quiet] You sheltered me."],
  Shirogane_Intro_2: [ONI, "[quiet, searching] ...I remember a flute."],
  Shirogane_Phase_2: [ONI, "[low, steady] Then remind me."],
  Shirogane_Defeat_2: [ONI, "[grieving, quiet] Shirogane... [softly] I'm sorry I forgot."],
  Kagetsune_Intro_2: [ONI, "[low, cold] It stays silent."],
  Kagetsune_Phase_2: [ONI, "[quiet, hard] Not any more."],
  Kagetsune_Defeat_2: [ONI, "[quiet, final] They're done ringing for you."],
  Akagane_Meet_Angry: [ONI, "[quiet, surprised] Warm... [low] Someone in there is still angry."],
  Akagane_Meet_Copper_3: [ONI, "[quiet, a little ashamed] ...I did."],
  Akagane_Meet_Doors_3: [ONI, "[low, gentle] You couldn't have known."],
  Akagane_Meet_Salute_3: [ONI, "[quiet, steady] When this is done."],
  Mizore_Meet_Steady: [ONI, "[quiet, calming] Cold hands. Steady ones."],
  Mizore_Meet_Collarbone_2: [ONI, "[quiet, apologetic] ...I don't remember."],
  Mizore_Meet_Oath_3: [ONI, "[quiet, moved] Did she."],
  Hibiki_Meet_Out: [ONI, "[low, wry, tired] It wants out as badly as I do."],
  Hibiki_Meet_Blame_3: [ONI, "[quiet, gentle] Hibiki."],
  Hibiki_Meet_River: [ONI, "[slow, remembering, tender] The drum by the river. You played it until your hands bled. [quiet] I remember."],

  // The souls who catch the dead for Oni (Story/Souls.md), from Dialogue.cs's SoulPickup conversations. Ids follow
  // Conversation.VoiceId, skipping the numbers of Oni's own lines above.
  Akagane_Meet_Angry_2: [AKAGANE, "[dry, gruff] Angry. Yes. That's the word for it. Hello, Guardsman."],
  Akagane_Meet_Angry_3: [AKAGANE, "[barking an order] Take the fire and burn them quick. They're only borrowed bodies."],
  Akagane_Meet_Copper: [AKAGANE, "[gruff, impatient] You don't remember me. Akagane. Captain of the vanguard."],
  Akagane_Meet_Copper_2: [AKAGANE, "[low, bitter] They called her silver. They called me copper. They called you demon, and you liked it."],
  Akagane_Meet_Men: [AKAGANE, "[low, bitter] Every samurai you cut down tonight was one of mine. I knew their names."],
  Akagane_Meet_Men_2: [AKAGANE, "[hard, quiet] So cut clean. Don't make them feel it twice."],
  Akagane_Meet_Doors: [AKAGANE, "[quiet, heavy, the anger gone] You led them up the mountain. I set my men at the hall doors. Facing out, as he ordered."],
  Akagane_Meet_Doors_2: [AKAGANE, "[quiet, heavy] We were guarding the wrong side of the door, Guardsman. Both of us."],
  Akagane_Meet_Doors_4: [AKAGANE, "[quiet, bitter, breaking] Neither could you. That's what I can't forgive."],
  Akagane_Meet_Bellringers: [AKAGANE, "[gruff, proud] Those bell-bearers at the shrine were my watch. Good men. Their bells drive the rest."],
  Akagane_Meet_Bellringers_2: [AKAGANE, "[barking an order] Silence them first. My men stop running at you when the ringing stops."],
  Akagane_Meet_Salute: [AKAGANE, "[grudging] I've decided something. When this is done, I'm going to salute you."],
  Akagane_Meet_Salute_2: [AKAGANE, "[dry, gruff] Once. Don't get used to it."],
  Akagane_Pickup_DiedBenibana: [AKAGANE, "[scornful] Her smoke again? Hold your breath and cut through it. That's all I'd have needed."],
  Akagane_Pickup_DiedUtsusemi: [AKAGANE, "[gruff, impatient] The clockwork had you counted. Stop standing where it expects."],
  Akagane_Pickup_DiedShirogane: [AKAGANE, "[low, grudging] She beat you. She beat me too, for twenty years. It doesn't get easier. You get better."],
  Akagane_Pickup_DiedKagetsune: [AKAGANE, "[gruff, grudging respect] You reached him. Further than my whole vanguard got. Now go further."],
  Akagane_Pickup_DiedArmy: [AKAGANE, "[dry, proud] My men put you down last night. Don't expect an apology. They were good at their work."],
  Akagane_Pickup_FirstClear: [AKAGANE, "[low, awed] He fell. I felt it. Every bell on him shook."],
  Akagane_Pickup_FirstClear_2: [AKAGANE, "[dry, tired] And here we all are again. So. Again, Guardsman."],
  Akagane_Pickup_Cleared: [AKAGANE, "[fierce] You broke him last night. My men felt the chains slack. Do it again."],
  Akagane_Pickup_HoldsFrost: [AKAGANE, "[dry, scornful] You've been to the physician. She'll have told you to rest. Ignore her."],
  Akagane_Pickup_HoldsStorm: [AKAGANE, "[grudging, gruff] That's the drummer's spark on you. Speed. Hm. My men marched up the mountain to her beat."],
  Akagane_Pickup_ManyRuns: [AKAGANE, "[low, weary] My men have marched every night since the Night. So have you. I'd stopped counting. You haven't stopped coming."],
  Akagane_Pickup_VeryManyRuns: [AKAGANE, "[dry, grudging humour] Fifty nights, give or take. I'd have promoted you by now. Don't let it go to your head."],
  Akagane_Pickup_ManyDeaths: [AKAGANE, "[dry, scornful] Five times the dancer. My vanguard only needed the once."],
  Akagane_Pickup_Offering: [AKAGANE, "[gruff, grudging] Nothing left to burn. Fine. Have the warmth, then."],
  Akagane_Greet_Lit: [AKAGANE, "[barking an order] Fire's lit, Guardsman. Use it."],
  Akagane_Greet_Bodies: [AKAGANE, "[low, bitter] Burn the bodies. Not the men."],
  Akagane_Greet_Vanguard: [AKAGANE, "[barking an order] Vanguard, forward. That's you now."],
  Akagane_Greet_Standing: [AKAGANE, "[dry, grudging] Hm. Still standing. Good."],
  Akagane_Greet_Waiting: [AKAGANE, "[gruff, impatient] My men are waiting. Don't keep them."],
  Akagane_Greet_Change: [AKAGANE, "[gruff, grudging] Take it, before I change my mind."],
  Akagane_Greet_Copper: [AKAGANE, "[dry] Copper's still warm."],
  Akagane_Greet_Clean: [AKAGANE, "[barking an order] Cut clean. Burn quick."],
  Mizore_Meet_Steady_2: [MIZORE, "[clinical] Hold still. I can't treat a man who flinches."],
  Mizore_Meet_Steady_3: [MIZORE, "[quiet, satisfied] There. The cold will keep it clean."],
  Mizore_Meet_Collarbone: [MIZORE, "[dry] Mizore. Physician to the court. I set your collarbone when you were sixteen. You were very rude about it."],
  Mizore_Meet_Collarbone_3: [MIZORE, "[quiet, clinical, sad] No. The coffin takes the small things first. I've been watching it take yours."],
  Mizore_Meet_Smoke: [MIZORE, "[clinical, remembering] I knew that smoke by its smell. Datura, and something sweeter."],
  Mizore_Meet_Smoke_2: [MIZORE, "[quiet, bitter] By the time I'd named it, I couldn't lift my hands. I had never been too slow before."],
  Mizore_Meet_Prince: [MIZORE, "[softer, remembering] I sat with his father through the whole of his dying. The boy held my sleeve all night."],
  Mizore_Meet_Prince_2: [MIZORE, "[softer, tender] He asked if it would happen to him. I told him the truth. Everyone dies, my prince. Even emperors."],
  Mizore_Meet_Prince_3: [MIZORE, "[quiet, tired, guilty] I think I forged the first piece of that armor, that night."],
  Mizore_Meet_Smiths: [MIZORE, "[clinical, grave] Six smiths came to me coughing ash, the year before the coronation. All dead by spring."],
  Mizore_Meet_Smiths_2: [MIZORE, "[quiet] I told no one at court. I told Shirogane, when she came asking. Then she went looking for you."],
  Mizore_Meet_Oath: [MIZORE, "[quiet] So she gave you the oath back."],
  Mizore_Meet_Oath_2: [MIZORE, "[softer, remembering] She came to me after you'd sworn it. She looked lighter than I'd seen her in years. She said you'd hesitate, and she'd forgive it."],
  Mizore_Meet_Oath_4: [MIZORE, "[dry, warm underneath] She did. Now stop hesitating. That's my prescription."],
  Mizore_Pickup_DiedBenibana: [MIZORE, "[clinical] Her smoke again. Shallow breaths, Oni. The dose is in the depth."],
  Mizore_Pickup_DiedUtsusemi: [MIZORE, "[dry] The clockwork keeps perfect time. You don't have to. Be early."],
  Mizore_Pickup_DiedShirogane: [MIZORE, "[clinical] She cut you to the bone and left every vital alone. That isn't an accident. It's a lesson."],
  Mizore_Pickup_DiedKagetsune: [MIZORE, "[quiet, pitying] He's never lost anything in his life except his father. Take something from him. He won't know what to do."],
  Mizore_Pickup_DiedArmy: [MIZORE, "[dry, disapproving] Arrows. Of all things. You were careless, and you know it."],
  Mizore_Pickup_FirstClear: [MIZORE, "[quiet, tired] He fell, and he got up again. Like his father, at the end. He'd hate to know that."],
  Mizore_Pickup_FirstClear_2: [MIZORE, "[softer] Again, then. I'll be here."],
  Mizore_Pickup_Cleared: [MIZORE, "[clinical, approving] You won last night. Your pulse is calmer for it. Keep it that way."],
  Mizore_Pickup_HoldsFire: [MIZORE, "[dry] You're running hot. That'll be the Captain. Let me take the edge off."],
  Mizore_Pickup_HoldsStorm: [MIZORE, "[softer, worried] You have Hibiki's spark in you. Mind it. She was always quicker than was good for her."],
  Mizore_Pickup_ManyRuns: [MIZORE, "[dry] Most patients die once. You've made a career of it."],
  Mizore_Pickup_VeryManyRuns: [MIZORE, "[dry, tired] I've stopped writing your case down. I ran out of paper in the coffin."],
  Mizore_Pickup_LowLives: [MIZORE, "[clinical, firm] One life left, and you're still picking fights. Sit. Let me see."],
  Mizore_Pickup_Offering: [MIZORE, "[quiet, tired] I've no more to teach your hands. I can still close a wound."],
  Mizore_Greet_Still: [MIZORE, "[clinical] Hold still."],
  Mizore_Greet_Breathe: [MIZORE, "[quiet, calming] Breathe slower. There."],
  Mizore_Greet_Clean: [MIZORE, "[clinical] Cold keeps a wound clean."],
  Mizore_Greet_Bleeding: [MIZORE, "[dry, tired] You're bleeding again. Of course you are."],
  Mizore_Greet_Hands: [MIZORE, "[softer] Steady hands, Oni. Borrow mine."],
  Mizore_Greet_Pulse: [MIZORE, "[quiet, clinical] Your pulse is racing. Let me slow it."],
  Mizore_Greet_Patient: [MIZORE, "[tired] Another night. Another patient."],
  Mizore_Greet_Thanks: [MIZORE, "[dry] Don't thank me. Survive."],
  Hibiki_Meet_Out_2: [HIBIKI, "[excited, fast] Out! Yes! Out, out, out, finally somebody listens!"],
  Hibiki_Meet_Out_3: [HIBIKI, "[gasping, overjoyed] Wait. Oni? It's you! It's really you!"],
  Hibiki_Meet_Name: [HIBIKI, "[teasing, a little hurt] You don't know my name, do you."],
  Hibiki_Meet_Name_3: [HIBIKI, "[fast, hopeful] Hibiki. The festival drum? I followed you to the river every day for a whole summer?"],
  Hibiki_Meet_Name_4: [HIBIKI, "[cheerful, brushing it off] It's fine! I'll tell you every time. I'm good at repeating things. I'm a drummer."],
  Hibiki_Meet_Procession: [HIBIKI, "[excited, fast] I drummed the whole way up the mountain. Everyone kept saying, our Oni sent for us! The whole village, in our best!"],
  Hibiki_Meet_Procession_2: [HIBIKI, "[warm, proud, a little wistful] Ma cried, she was so proud of you."],
  Hibiki_Meet_Procession_3: [HIBIKI, "[voice catching, then teasing] Don't. Don't make that face. I'll hit you with a stick."],
  Hibiki_Meet_OneBell: [HIBIKI, "[serious, stopping short, clear] He gave the court bells. He gave the soldiers bells. He didn't give us any."],
  Hibiki_Meet_OneBell_2: [HIBIKI, "[serious, steady] The whole village rings inside the coffin. All of us, one sound. Listen for it when he swings that hammer."],
  Hibiki_Meet_Blame: [HIBIKI, "[serious, blunt] I told you I don't blame you. That was a lie."],
  Hibiki_Meet_Blame_2: [HIBIKI, "[earnest, voice catching] I do. A little. Every night. And I'd follow you up there again anyway. That's the stupid part."],
  Hibiki_Meet_Blame_4: [HIBIKI, "[stunned, wondering] ...You said my name."],
  Hibiki_Meet_River_2: [HIBIKI, "[amazed, voice catching] ...You remembered on your own."],
  Hibiki_Meet_River_3: [HIBIKI, "[laughing through tears, teasing] Don't you dare forget it again, my lord guardsman."],
  Hibiki_Pickup_DiedBenibana: [HIBIKI, "[teasing, fast] She got you with the sleepy smoke again? Stay awake! Do what I do. Never stop moving!"],
  Hibiki_Pickup_DiedUtsusemi: [HIBIKI, "[serious, then fierce] The tea monk wouldn't hurt anybody. That thing would. Be faster than its ticking!"],
  Hibiki_Pickup_DiedShirogane: [HIBIKI, "[matter-of-fact, admiring] Lady Shirogane beat you? Of course she did. She's Lady Shirogane."],
  Hibiki_Pickup_DiedKagetsune: [HIBIKI, "[serious, voice catching] I heard the village ring when he hit you. All of us at once. ...Get up, Oni. Please."],
  Hibiki_Pickup_DiedArmy: [HIBIKI, "[teasing, laughing] Just soldiers? Just soldiers got you? Captain Kettle's going to be unbearable."],
  Hibiki_Pickup_FirstClear: [HIBIKI, "[excited, fast, laughing] You did it! You did it, you did it! The whole coffin shook!"],
  Hibiki_Pickup_FirstClear_2: [HIBIKI, "[deflating, serious] ...And we're all still in here. Oh."],
  Hibiki_Pickup_FirstClear_3: [HIBIKI, "[bright again, determined] That's fine! We'll do it again, louder!"],
  Hibiki_Pickup_Cleared: [HIBIKI, "[excited] Again! Like last night! Ba-dum, down he goes!"],
  Hibiki_Pickup_HoldsFire: [HIBIKI, "[teasing] You smell like smoke. Was that Captain Kettle? He's so grumpy. Here, have something fun."],
  Hibiki_Pickup_HoldsFrost: [HIBIKI, "[bright, then fond] Grandmother's been looking after you! She held my wrist, at the end. She's good at that."],
  Hibiki_Pickup_ManyRuns: [HIBIKI, "[playful] I've been counting your nights in drumbeats. I've run out of rhythms. I'm making new ones."],
  Hibiki_Pickup_VeryManyRuns: [HIBIKI, "[proud, warm] Everyone in the coffin knows your name now. Even the ones who never met you. I told them all."],
  Hibiki_Pickup_Offering: [HIBIKI, "[sheepish, laughing] I'm all out of thunder! Um. Here's a hug instead. It's a soul hug. It counts."],
  Hibiki_Greet_Hi: [HIBIKI, "[excited, calling out] Oni! Over here! Hi!"],
  Hibiki_Greet_Faster: [HIBIKI, "[excited, fast] Faster, faster, faster!"],
  Hibiki_Greet_Thunder: [HIBIKI, "[playful] Ba-dum! That's thunder. That's me."],
  Hibiki_Greet_Keep: [HIBIKI, "[teasing] Try to keep up, my lord guardsman!"],
  Hibiki_Greet_Cheer: [HIBIKI, "[bright] The village is cheering for you. Can you hear it?"],
  Hibiki_Greet_Stop: [HIBIKI, "[fast] Don't stop! Stopping's the worst."],
  Hibiki_Greet_Catch: [HIBIKI, "[excited, calling out] Here! Catch!"],
  Hibiki_Greet_Drumroll: [HIBIKI, "[playful, teasing] Drumroll, please... It's me again!"],

  // Poguru, the Lost Dragon (Story/Poguru.md): the tutorial, his barks and his temple talk. Ids follow
  // Conversation.VoiceId; Oni's replies inside his conversations are in Oni's voice. Poguru_Wake_Count fills in a
  // death count, so it stays a subtitle.
  Poguru_Tut_Wake: [POGURU, "[panicked, squeaky shout] ONI! ONI! WAKE UP! [terrified, fast] Scary dead soldiers are coming!"],
  Poguru_Tut_Wake_2: [ONI, "[low, tired, flat] ...Who are you."],
  Poguru_Tut_Wake_3: [POGURU, "[proud, puffed up] Poguru! The Lost Dragon! [giggles] I live in your wisp. Rent-free!"],
  Poguru_Tut_Wake_4: [POGURU, "[gasps, fast, breathless] No time! Do you remember how to fight?"],
  Poguru_Tut_Again: [POGURU, "[excited, fast] Oni! Wake up! Soldiers! Again! [eager] Want the lesson again? [giggles] I LOVE the lesson."],
  Poguru_Tut_Yay: [POGURU, "[cheering] Yay! [nervous, fast] Stay close. They're coming!"],
  Poguru_Tut_Skip: [POGURU, "[fast, eager] Okay okay okay! I'll be in your wisp. Cheering. [whispering] Quietly. [giggles] Mostly."],
  Tut_Soldiers: [SOLDIER, "[hollow, rasping, dry rattle of breath] There he is... [sneering, a dead man's laugh] the Emperor's runaway dog."],
  Tut_Soldiers_2: [SOLDIER, "[hollow, rasping, menacing] The coffin wants its demon back. [slow, a dry croak] Come... quietly."],
  Tut_Soldiers_3: [ONI, "[low, tired, flat] ...No."],
  Tut_Soldiers_4: [POGURU, "[squeaks] Eep! [panicked, fast] Five of them! [flustered, trying to stay calm] Okay, okay, one at a time!"],
  Tut_Slash_Taunt: [SOLDIER, "[rasping, snarling shout] Join us, traitor!"],
  Poguru_Tut_SlashIt: [POGURU, "[urgent, fast] An arrow! SLASH it! [excited] Your cut breaks it clean in half!"],
  Poguru_Tut_TakeHim: [POGURU, "[fierce, cheering] Now take him down!"],
  Tut_Slash_Fall: [SOLDIER, "[death rattle, wheezing, fading] Not... again...!"],
  Tut_Charge_Taunt: [SOLDIER, "[hoarse, rasping battle shout] Drag him down! Together!"],
  Poguru_Tut_ShurikenIt: [POGURU, "[urgent, fast] Two in a row! [excited] Throw your SHURIKEN, it flies right through them both!"],
  Poguru_Tut_Stopped: [POGURU, "[gleeful] Stopped in their tracks! [cackles] Ha!"],
  Tut_Spear_Taunt: [SOLDIER, "[rasping, smug, sneering] Let's see you cut THIS. [proud, gloating] Blessed steel from the Emperor's own forge!"],
  Poguru_Tut_ParryIt: [POGURU, "[panicked, very fast] That spear's cursed! Kagetsune's blacksmiths forge them with nasty magic. [frantic] You can't cut it or break it. [shouting] PARRY IT!"],
  Tut_Flee: [SOLDIER, "[rasping, terrified, stammering] Th-that's not natural! [screaming, running away] CAPTAAAIN!"],
  Tut_Flee_2: [SOLDIER, "[rasping whimper, higher pitched, cowardly wail] Wait for meee!"],
  Tut_Flee_3: [POGURU, "[giggling, taunting] Hehe! Run away! [pause, suddenly worried] ...Wait. Where are they running TO?"],
  Poguru_Tut_Backup: [POGURU, "[dismayed, small voice] Uh oh. [gulps] He brought a LOT of backup."],
  Poguru_Tut_Backup_2: [ONI, "[low, tired, flat] ...Good."],
  Poguru_Tut_Backup_3: [POGURU, "[nervous, then determined] Okay! I'll bonk every arrow I can. [proud] I'm a very good bonker. [brave] You do the rest!"],
  Poguru_Tut_Ultimate: [POGURU, "[cheerful, generous] You're doing great! [excited] Here, have some of my soul-fire!"],
  Poguru_Tut_UltimateIt: [POGURU, "[urgent, thrilled] Your medallion's full! [shouting] Let your SHADOW out!"],
  Poguru_Tut_Clone: [POGURU, "[amazed gasp] Two Onis! [giggly] It copies everything you do. Just weaker. [self-deprecating laugh] Like me!"],
  Poguru_Block_15: [POGURU, "[straining, shouting through effort] I'M BONKING SO HARD RIGHT NOW!"],
  Poguru_Block_20: [POGURU, "[exasperated, whiny shout] Oni! Swing the sword! The SWORD!"],
  Poguru_Block_30: [POGURU, "[dizzy, cross, groaning] My head is RINGING!"],
  Poguru_Block_45: [POGURU, "[shaking with fury, about to snap] I am going to EXPLODE!"],
  Poguru_Block_More: [POGURU, "[furious, squeaky shouting] BONK! BONK! BOOOONK!"],
  Poguru_Rage_Charge: [POGURU, "[growling, building to a roar] THAT'S IT! [louder, furious] THAT IS IT!! [roaring] RAAAAAGH!!!"],
  Poguru_Tut_Raged: [POGURU, "[frantic yelling] ONI!! WERE YOU NAPPING?!"],
  Poguru_Tut_Raged_2: [POGURU, "[furious, squeaky] I did EVERYTHING! [indignant shout] I'm a SUPPORT dragon! SUPPORT!"],
  Poguru_Tut_Raged_3: [ONI, "[low, tired, flat] ...You were doing fine."],
  Poguru_Tut_Raged_4: [POGURU, "[huffy, pouting] HMPH!"],
  Poguru_Block_1: [POGURU, "[proud, quick] Bonk! Got it!"],
  Poguru_Block_3: [POGURU, "[wincing, then brave] Ow. Okay. Still fine!"],
  Poguru_Block_6: [POGURU, "[whiny, exasperated] Oni, you can ALSO move, you know!"],
  Poguru_Block_10: [POGURU, "[whiny, indignant shout] My HEAD is not a SHIELD!"],
  Poguru_Tut_Win: [POGURU, "[overjoyed, giggling] We did it! Victory wiggle! [singsong] Wiggle wiggle wiggle..."],
  Poguru_Tut_LookOut: [POGURU, "[panicked, squeaky shout] LOOK OUT!!!"],
  Poguru_Tut_Hearts: [POGURU, "[wailing, panicked] Waaah! It went right through me! [worried, fast] Are you okay?!"],
  Poguru_Tut_Hearts_2: [ONI, "[low, tired, flat] ...Fine."],
  Poguru_Tut_Hearts_3: [POGURU, "[explaining, earnest] Those hearts in the corner are your lives. Lose them all and it's back to the temple. [whiny] It's damp there."],
  Poguru_Tut_Hearts_4: [POGURU, "[excited, proud] Good news! I've been saving a heart soul. For emergencies. [gasps] This is an emergency!"],
  Poguru_Tut_Hearts_5: [POGURU, "[cheerful] Hearts fall out of broken cover sometimes. [conspiratorial] Once in a blue moon a soldier drops one too!"],
  Poguru_Tut_Collect: [POGURU, "[excited, urging] Go get it! It's on your row!"],
  Poguru_Tut_Collected: [POGURU, "[happy] Yummy! [flustered] I mean... healthy!"],
  Poguru_Tut_Reward: [POGURU, "[awed, delighted] Ooooo! Pretty cool soul!"],
  Poguru_Tut_Reward_2: [POGURU, "[puzzled, softer] Wait... it looks like someone familiar..."],
  Poguru_Tut_Boons: [POGURU, "[excited, explaining] Souls lend you their power. That's a boon!"],
  Poguru_Tut_Boons_2: [POGURU, "[earnest] Pick ONE. Look them all over, [happy] then take the one you like best!"],
  Poguru_Tut_Boons_3: [POGURU, "[explaining] They change your slash, shuriken or parry. [giggles] Or pick the shiniest. That's what I'd do."],
  Poguru_Tut_Paths: [POGURU, "[excited] Paths! Each leads to your next fight. What's floating in it is your prize for winning."],
  Poguru_Tut_Paths_2: [POGURU, "[earnest] Walk under the one you want, and off we go! [conspiratorial] After a few rounds, one of them leads to the boss!"],
  Poguru_Tut_Done: [POGURU, "[proud, delighted] Looks like you DO remember how to fight!"],
  Poguru_Tut_Done_2: [POGURU, "[excited, fast] Let's hurry and free everyone!!"],
  Poguru_Tut_Done_3: [ONI, "[low, tired, flat] ...Back in the wisp."],
  Poguru_Tut_Done_4: [POGURU, "[fast, flustered] Going! Shutting up! [giggles] ...Mostly!"],
  Poguru_Utsu_Sword: [POGURU, "[panicked, squeaky shout] Laser! [fast, urgent] Oni, hurry! Hide behind the sword!"],
  Poguru_Utsu_Reclaim: [POGURU, "[panicked squeaky shout, very fast] Sword! MOVE!"],
  Poguru_Utsu_Rock: [POGURU, "[terrified gasp] That's a BIG one! [panicked, fast] Behind a rock, quick!"],
  Poguru_Round_Go: [POGURU, "[cheering, fast] Go go go!"],
  Poguru_Round_Snacks: [POGURU, "[whiny] More of them! [curious] Do they ever bring snacks?"],
  Poguru_Round_Wave: [POGURU, "[cheerful, waving] Hi, army! He's going to beat you now! [sheepish] ...Sorry!"],
  Poguru_Round_Breathe: [POGURU, "[earnest, coaching] Deep breaths! Like me! [panting quickly] Hff hff hff."],
  Poguru_Round_Count: [POGURU, "[nervous] I counted them. [gulps] There's... a lot."],
  Poguru_Soul_Shiny: [POGURU, "[gasps, delighted] Shiny! Can I hold it? [deflated] No? Okay."],
  Poguru_Soul_Thanks: [POGURU, "[excited] A friend from the coffin! [scolding, cute] Say thank you, Oni!"],
  Poguru_Soul_Glow: [POGURU, "[delighted] You're glowing! I'm glowing too. [proud, solemn] Solidarity."],
  Poguru_Boss_Benibana: [POGURU, "[relieved, cheerful] Smoke lady's gone! [proud whisper] I kept one petal."],
  Poguru_Boss_Utsusemi: [POGURU, "[relieved] The ticking stopped! [hopeful] Is it lunchtime?"],
  Poguru_Boss_Shirogane: [POGURU, "[sad, quiet] She looked so sad... [sniffles] Wisps can't cry. I checked."],
  Poguru_Boss_Won: [POGURU, "[cheering shout] WE WON! [flustered] I mean, you won. I helped. [proud] Emotionally."],
  Poguru_Boss_Wiggle: [POGURU, "[giggles, excited] Victory wiggle! [deflated] ...No? Okay."],
  Poguru_Wake_First: [POGURU, "[whiny] Back in the damp temple. [soft, warm] ...But I like that you're here."],
  Poguru_Wake_Army: [POGURU, "[whiny, rubbing his head] Those soldiers were VERY pointy today."],
  Poguru_Wake_Benibana: [POGURU, "[sniffling] Her smoke made me sneeze."],
  Poguru_Wake_Utsusemi: [POGURU, "[sing-song, then wincing] Tick, tock, ow."],
  Poguru_Wake_Shirogane: [POGURU, "[whiny, awed] She hits SO hard."],
  Poguru_Wake_Kagetsune: [POGURU, "[small, ashamed] I hid in your wisp the whole time. [sheepish] I'm not proud."],
  Poguru_Wake_Up: [POGURU, "[cheerful, encouraging] Up you get! The pool's warm. [giggles] Ish."],
  Poguru_Wake_Hug: [POGURU, "[warm, happy] Wisp hug! Very warm. [giggles] Very small."],
  Poguru_Talk_Who: [ONI, "[low, tired, flat] Where did you come from."],
  Poguru_Talk_Who_2: [POGURU, "[happy, fast] Your wisp! The left one. It's cosy. [thoughtful] I think I was always there, I just woke up late."],
  Poguru_Talk_Who_3: [POGURU, "[proud] I'm a dragon! [unsure] Probably. A small one. Lost. [proud] That's my title!"],
  Poguru_Talk_Cape: [ONI, "[low, quiet, flat] That red cloth."],
  Poguru_Talk_Cape_2: [POGURU, "[proud, then curious] My cape! It smells like festival lanterns. Do you know why?"],
  Poguru_Talk_Cape_3: [ONI, "[low, tired, a pause before it] ...No."],
  Poguru_Talk_Remember: [POGURU, "[earnest, kind] Every time you wake up, you forget a little. So I remember for you!"],
  Poguru_Talk_Remember_2: [POGURU, "[sheepish giggle] Mostly the wrong things. [curious] Did you used to like plums?"],
  Poguru_Talk_Remember_3: [ONI, "[low, quiet, remembering] ...I did."],
  Poguru_Talk_Silver: [POGURU, "[curious, gentle] When the silver lady's near, your wisps go all warm. Is she a friend?"],
  Poguru_Talk_Silver_2: [ONI, "[low, quiet, aching] ...She was."],
  Poguru_Talk_Dawn: [POGURU, "[awed, excited] The sun! I'd never seen it! It's very bright. [sheepish] I hid. [giggles] But I peeked!"],
  Poguru_Talk_Nap: [POGURU, "[sleepy yawn] I was napping. In your wisp. Which is in you. [smug giggle] So technically you were napping too."],
  Poguru_Talk_Rawr: [POGURU, "[tiny growl] Rawr. [proud] That's my fierce face. [hopeful] Was it fierce? [flustered, fast] Don't answer."],
  Poguru_Talk_GhostFire: [POGURU, "[thoughtful, matter-of-fact] I tried eating a ghost-fire. [disgusted] It tasted like Tuesday."],
  Poguru_Talk_Flute: [POGURU, "[hopeful] Play the flute later? [happy] The wisps dance when you do."],
  Poguru_Talk_Flute_2: [ONI, "[low, tired, a little softer] ...Maybe."],
};

const args = process.argv.slice(2);
const fxOnly = args.includes("--fx-only");
const wanted = args.filter((a) => a !== "--fx-only");
const names = wanted.length > 0 ? wanted : Object.keys(LINES).filter((n) => !fxOnly || FX[LINES[n][0]]);
const client = fxOnly ? null : new ElevenLabsClient();

// Writes the clip from its raw take through the voice's filter.
function bake(raw, out, filter) {
  execFileSync("ffmpeg", ["-v", "error", "-y", "-i", raw, "-filter_complex", filter, "-c:a", "libmp3lame", "-b:a", "128k", out]);
}

await mkdir(OUT_DIR, { recursive: true });
await mkdir(RAW_DIR, { recursive: true });
for (const name of names) {
  const line = LINES[name];
  if (!line) {
    console.error(`Unknown line "${name}". Known: ${Object.keys(LINES).join(", ")}`);
    process.exitCode = 1;
    continue;
  }

  const [voice, text] = line;
  const filter = FX[voice];
  const file = new URL(`${name}.mp3`, OUT_DIR);
  const raw = new URL(`${name}.mp3`, RAW_DIR);
  try {
    if (fxOnly) {
      if (!filter) continue;
      // A clip voiced before baking existed is its own raw take.
      if (!existsSync(raw)) await copyFile(file, raw);
    } else {
      console.log(`Voicing ${name}...`);
      const audio = await client.textToSpeech.convert(voice, {
        text,
        modelId: "eleven_v3",
        outputFormat: "mp3_44100_128",
      });
      const source = typeof audio.pipe === "function" ? audio : Readable.fromWeb(audio);
      await pipeline(source, createWriteStream(filter ? raw : file));
    }
    if (filter) bake(raw.pathname, file.pathname, filter);
    console.log(`Saved ${file.pathname}${filter ? " (baked)" : ""}`);
  } catch (err) {
    console.error(`Error for ${name}:`, err.message ?? err);
    process.exitCode = 1;
  }
}
