# Character artwork guide

This is the shared art direction for Benibana, Utsusemi, Shirogane, Kagetsune, and future boss illustrations. Consult this file before generating or revising character artwork. Current user direction takes precedence; update this guide when a design decision changes.

## Visual direction

- Detailed anime dark fantasy illustration: refined faces, flowing hair and fabric, ornate armor, readable silhouettes, and carefully drawn metalwork.
- Preserve each character's identity across sprites, full-body illustrations, close-ups, and phases. Translate sprite features into illustration detail without inventing a different character.
- Use Hades-inspired dramatic framing, graphic shadow shapes, strong contrast, and dynamic poses for Phase 2 introductions. Keep this project's existing detailed rendering style.
- Phase 2 must communicate an escalation through pose, silhouette, weapon state, and character-specific effects. A color filter alone is insufficient.
- Keep anatomy, hand grips, weapon construction, costume motifs, and accessories consistent between paired images.
- Deliver artwork without baked-in names, captions, interface elements, watermarks, or contact-sheet panels.

## Required four-piece set

| Deliverable | Exact width × height | Composition |
| --- | --- | --- |
| Phase 1 full body | 1024 × 1536 px | Entire character, boots, weapons, hair, and costume within canvas |
| Phase 1 close-up | 1024 × 1536 px | Transparent dialogue portrait (Hades-style), cut off around the thigh; eyes about 0.19 down the canvas so the set lines up; nothing clipped at the top or sides. Exceptions: Shirogane's is 1536 × 1024 for the width of her crossed blades (same framing and eye line), and Kagetsune's is a 1536 × 1024 low-angle bust, kept by choice |
| Phase 2 full body | 1024 × 1536 px | Entire transformed character and essential spirit/effect silhouette |
| Phase 2 close-up | 1536 × 1024 px | Dramatic introduction framing matching the Phase 2 full-body design |

Dimensions mean the actual saved PNG canvas, not merely the requested aspect ratio. Verify every export. Preserve proportions when fitting artwork; do not stretch the character. Leave margins around full-body silhouettes rather than clipping boots, sword tips, or ornaments. Keep full-body transparent canvases at the specified size rather than trimming to visible pixels.

Full-body exports should have a clean transparent background. No blurred backdrop, fog wash, bloom halo, depth-of-field blur, or motion blur. Hair, armor, cloth, weapons, and spirit-flame edges must remain crisp. Show spectral material through defined translucent shapes and negative space, not soft-focus smearing. Phase 1 close-ups are transparent too, since they double as dialogue portraits over any scene. Phase 2 close-ups may have opaque cinematic backgrounds, but faces and defining details must remain readable.

The earlier Benibana and Utsusemi Phase 2 full-body exports have irregular dimensions (1047 × 1502 and 1065 × 1477). They are historical sources, not size templates. Standardized exports use 1024 × 1536; Phase 2 close-ups use 1536 × 1024.

## Benibana — crimson swordswoman

Identity: adult woman with long vivid red hair, recognizable face, black and crimson kimono, silver cross-shaped ornaments and chains, tattoos, red gloves, and an ornate katana/scabbard. Preserve the red/black/silver palette and embroidered costume language.

Phase 1: poised and controlled, gathered high ponytail, intact ceremonial presentation, restrained weapon posture. Use the existing Phase 1 portraits as references.

Phase 2 working direction, **Crimson Unsealed**: loosened sweeping hair, glowing crimson tattoos, lifted chains and charms, battle-worn sleeves, drawn dark katana with a narrow crimson edge, and a more active stance. Keep her face and core costume recognizable. Match these changes in the close-up.

References: `Assets/Art/Portraits/Benibana_Full.png`, `Assets/Art/Portraits/Benibana_Closeup.png`, and `output/phase2-artwork/crimson-*.png`.

## Utsusemi — ceremonial automaton

Identity: tall slender black articulated automaton, enormous circular hat, blue and ivory robes with gold veining, hanging gold ornaments, vertical orange face slit, orange chest core, and ornate cane sword. Maintain the blue/ivory/gold materials even when orange energy intensifies.

Phase 1: composed ceremonial stance, intact hat and robes, contained face/core illumination, cane-like weapon presentation.

Phase 2 working direction, **Fractured Overdrive**: hat sections separate into a recognizable broken circular halo; chest armor opens around a brighter core; robe tears reveal mechanical anatomy; the sword gains an incandescent orange edge. Preserve the same body, materials, and ornament vocabulary. Match the fracture pattern and core state between full body and close-up.

References: `Assets/Art/Portraits/Utsusemi_Full.png`, `Assets/Art/Portraits/Utsusemi_Closeup.png`, and `output/phase2-artwork/automaton-*.png`.

## Shirogane — stage 3 ghost samurai

### Locked user requirements

- Palette correction: dominant black, cobalt blue, white, and cyan. Gold is VERY MINIMAL, limited to small muted fittings/fasteners; never broad armor borders, guards, gold tracery, or gold embroidery. Large metal surfaces are dark steel with restrained silver-gray edge highlights. Earlier gold-heavy exports are superseded in palette by `output/shirogane-palette-corrected-v1/`.
- Approved fresh direction: `output/shirogane-fresh-armored/shirogane-phase1-full-body-review-v2.png`. Preserve its mature proportions and closed samurai armor across the set. Current exports and prompts are in `output/shirogane-approved-set-v1/`; Phase 2 full-body `-v2` is the wider-framed selection.
- Phase 1 close-up must also have genuine transparency, per user correction. Phase 2 close-up may retain its cinematic background.
- Fresh artwork must use the pixel sprite as Shirogane's design authority; do not use rejected Shirogane illustrations as references. Approved other bosses define rendering quality only.
- Preserve the sprite's protective armor coverage: closed armored torso, substantial shoulder and hip plates, covered legs, and practical armored boots. Avoid cleavage, exposed thighs, corset styling, and high heels. The user rejected the sexualized interpretation.
- **Ghostly WHITE skin in BOTH phases and ALL four artworks.** This includes face, ears, lips, neck, shoulders, chest, arms, and visible legs. Use porcelain/chalk-white highlights with restrained neutral cool-gray shading.
- Do not give her peach/pink living skin, rosy blush, or an overall blue/lavender skin fill. Blue eyes, armor, weapons, and spirit effects remain blue. Blue environmental light must not overwhelm the white skin identity.
- **No blur effect on either full-body artwork.** Keep the character and spectral wolf sharply defined, including hair, fabric, armor, weapons, and flame contours. Remove blurry background gradients and diffuse blue haze.
- Preserve her white flowing hair, blue eyes, blue-horned oni mask worn as a hair ornament, blue ribbons/tassels, and ornate black/cobalt/white/gold samurai costume. Do not turn the ornament into a mask covering her face.

Phase 1: solo ghost samurai, composed and restrained; no wolf. Maintain paired katana identity and follow the supplied sprite's cyan spirit-blade direction when reconciling older references.

Phase 2 working direction: the same ghost-white woman with the large spectral wolf behind her shoulder, stronger spirit-blade energy, and a more commanding presentation. The wolf is luminous pale-cyan spirit matter with flame-shaped ears/mane, clear cyan-white eyes, and a geometric diamond forehead marking. It is not an opaque furry animal. Keep its face readable and its spirit contours crisp. White skin does not change between phases.

Identity/composition references: `Assets/Art/Portraits/Shirogane_Full.png`, `Assets/Art/Portraits/Shirogane_Closeup.png`, and `output/ghostly-samurai/`. The user-supplied white-skinned pixel sprite is the skin authority.

**Historical-reference warning:** the existing portraits and older prompt files contain warm skin or instructions for blue/lavender skin and soft backgrounds. Those parts are superseded by the locked requirements above. Use them for identity and costume, not as authority for skin color or blur. Existing wolf close-ups at 1672 × 941 also need the standard 1536 × 1024 canvas.

## Kagetsune — the Thousand-Soul Throne

The Emperor and the planned Stage 4 final boss. Story and personality are in `Story/Kagetsune.md`; read it before designing a new pose or effect, because every motif on him means something (bells = sealed souls, teal fire = the souls themselves, oni skulls = mockery of the ronin Oni).

Identity: a colossal, broad-shouldered horned warlord, never shown unarmored or with a visible face. Closed, spiked black helm with a narrow visor slit and two large curved, ridged bronze horns. Blackened scale armor everywhere, edged and overlaid with pierced, thorny bronze filigree; a dark fur mantle over the right shoulder. Snarling bronze oni-skull faces with teal eyes on both pauldrons, the belt buckle and both knees. Braided crimson rope at the waist hung with **pierced bronze bells** on chains, small bronze crosses/charms and red tassels. Tattered crimson and off-white robes and long ofuda talisman strips (red sigils on cream paper) hanging from shoulders, belt and weapon; a central tasset banner bearing the imperial crest. Teal soul-fire with drifting skulls is part of his silhouette.

Weapon, the **coffin-hammer (Soul Coffin)**: carried resting on his right shoulder, gripped near the head-side of the shaft. The head is a tall coffin-shaped reliquary in dark latticed metal, chained shut and cross-bound with red cord and ofuda, with a bronze oni face set in its side; the shaft is wrapped in red cord with bronze bands and ends in a spiked bronze pommel hung with tassels and talismans. Keep its scale huge (the head is roughly torso-sized) and its construction identical between images.

Palette: black steel, aged bronze/brass, crimson and cream cloth, and **teal** (cyan-green) soul-fire. Red is for cloth, cords and his phase 1 eyes only. Teal is the only glow colour; no gold light, no blue. No moon imagery anywhere: his arena and backgrounds are starless black, smoke and crimson.

Phase 1, composed tyrant: standing square and planted, hammer at rest on his shoulder, talismans hanging still. **Red** eyes in the visor. The teal fire is contained, glowing through the open lattice of his chest with a skull or two inside it and only a few wisps escaping near the coffin. The bells hang quiet.

Phase 2 working direction, **The Unchained Wraith**: the same body, armor and weapon, now breaking apart from within. The coffin's chains snap and swing loose; broken chain links fly. The armor's scale cracks into a glowing teal lattice across chest, arms and legs, with screaming skull faces in the chest. **Eyes turn teal** (the souls are looking out). Horns and helm vent teal fire; skull spirits swarm free all around him, mouths open. Cloth and ofuda are torn and whipping in the surge. The close-up is a dynamic Hades-style framing: his free gauntlet thrusts toward the viewer, gold-tipped talons spread around a howling skull flame.

References: `Assets/Art/Portraits/Kagetsune_Full.png` (the user's concept art; design authority for identity, costume and weapon), `Kagetsune_Closeup.png` (calm bust, hammer at rest, red background smoke), `Kagetsune_Phase_Full.png` and `Kagetsune_Phase_Closeup.png` (the Unchained Wraith). All four are at the required sizes (1024 × 1536 / 1536 × 1024).

Known issue: `Kagetsune_Phase_Full.png` appears to carry a soft teal/crimson gradient backdrop rather than a clean transparent background. Next time it's revised, re-export it transparent and crisp per the four-piece set rules.

Pixel sprite: PixelLab character "Kagetsune P1" (pro mode from `Kagetsune_Full.png`, styled on Shirogane's sprite) and its "Unchained Wraith" state, five clips each (Idle, Slam, Toll, Hit, Defeat) in `Art/Sprites/Characters/Kagetsune[Phase2]/`. It keeps the horns, the hammer-on-shoulder silhouette, the bells at the waist and the teal chest glow readable at chibi scale, and is drawn about one and a half times the other bosses' size (`BossArtSetup.KagetsuneScale`). The Wraith state burns teal at the horns and through cracks in the armor, with skull spirits about him.

## Revision and new-character workflow

1. Read this guide and inspect the current reference images. Identify which features are locked and exactly what the user wants changed.
2. For a new character, establish identity, silhouette, palette, materials, weapon, costume motifs, and Phase 1/2 differences before expanding the set. Add the resulting direction here.
3. Produce or revise each phase's full body first; use it as the design reference for its close-up. If full body changes later, reconcile the paired close-up.
4. Use focused edits and explicitly preserve unrelated features. Do not let a background edit redesign a face or a skin correction recolor armor.
5. Save iterations separately. Keep historical sources and original game assets until a replacement is deliberately selected. Record final prompts alongside exports.
6. Verify pixel dimensions, full-body alpha/background, margins, anatomy, white-skin consistency where applicable, absence of blur, and correspondence between each full-body/close-up pair.
7. Inspect full resolution and a small preview: both detail quality and silhouette readability matter. Do not label a requested correction complete unless visible in the result.

Suggested filenames: `<character>-phase1-full-body.png`, `<character>-phase1-close-up.png`, `<character>-phase2-full-body.png`, `<character>-phase2-close-up.png`. Use a version suffix or new export folder for revisions. Generation exports are not automatically wired into Unity; integration is a separate explicit step.

## Reusable prompt outline

> Create/edit [character and deliverable] using [specific reference paths and their roles]. Preserve [face, hair, palette, costume, ornaments, weapons]. Change only [requested revision or phase escalation]. Match this guide's exact canvas size and framing. Full-body artwork must be crisp, with transparent background and no blur, haze, or bloom. For Shirogane, all exposed skin is ghostly WHITE in both phases, never peach or blue/lavender. Keep [phase-specific effects] consistent with its paired artwork. No text, UI, watermark, extra limbs, cropped boots, or clipped weapon tips.

