# The Dead Army

[Back to the story](README.md)

| | |
|---|---|
| Role | The "invaders": every round's army, the kite scouts, the tutorial's five soldiers. |
| Were | The court guard and Akagane's vanguard, who died on the shrine stair on the Night of a Thousand Bells. |
| Are | Their souls, sent back out by the coffin each night in the bodies they died in. |
| Want | Nothing of their own any more. The coffin wants Oni back, and they carry that want like an order. |
| Signature | Rotted armour, bone, grave-wrappings, a faint teal ember in each eye slit. |

## Who they are

On the Night of a Thousand Bells the army marched up the mountain behind Oni and stood guard while the coffin opened.
None of them came down. Their souls went into the armor and their bodies stayed where they fell, on the stair and
in the hall, under the stopped hour.

Each night the coffin sends the souls back out **in those same bodies**: armour rusted through, flesh gone grey and
tight on the skull or gone to bone, straw hats rotted, coats in rags. The only light in them is a faint teal ember in
each eye slit, their own sealed soul showing through (teal is soul-fire, see the consistency rules). When one is cut
down its body crumbles into bones and ash, the ember goes out, and the soul is dragged back into the coffin, which
knits it a body again out of grave-dust before the next night. That is why the army is always full.

They are hollowed out. What's left of them is scraps: a grunt's sneer, a captain's name, a coward's fear. They speak
the coffin's want ("the coffin wants its demon back") as if it were an order from the Emperor, because that is the
last order they remember. They are victims as much as the bosses are; every one Oni cuts down is a soul the coffin
gets back, unless a soul like Akagane catches it first ([Akagane.md](Akagane.md)).

## The kinds

| Kind (code) | Look | Story |
|---|---|---|
| Ashigaru | A skeletal foot soldier in a rotted jingasa straw hat and the rags of a clan coat, a rusted yari in hand. | The levied spearmen, the bulk of the vanguard. |
| Archer (`Bushi` art) | A corpse bushi in cracked lamellar, grey skin pulled tight to the skull, a warped yumi. | The court's bowmen. Three arrows at once, as they were drilled. |
| Taishō | A revenant general: horned kabuto, a cracked menpō mask over a bare skull, a tattered horo cape, the eye slits lit. | Akagane's officers. They still throw the Emperor's cursed spears. |
| Shieldbearer | A hulking dead man behind a temple door torn from its hinges, nailed with iron and plastered with ofuda. | They tore the hall's doors down to shield the line, and never put them down. |
| Yurei | An onryō: white burial kimono, long black hair over the face, no feet, sleeves trailing into mist. | The souls whose bodies are gone entirely, so the coffin doesn't bother giving them one. Their arrows are as bodiless as they are and pass through cover. |
| Bellringer | A hollow shrine watchman in rotted white and red, his jaw bound shut with cloth, a hand bell on a cord. | Akagane's shrine watch, dead at their posts. Their tolls drive the army to fire faster. |
| Scout | A shrouded corpse lashed to a torn, blood-stained paper kite, drifting across the top of the sky like a hanged man. | Scouts who flew the signal kites on the night. A soul always slips out of a downed one. |

## The tutorial's five

The five soldiers who find Oni first (speaker "Ashigaru", Story in [Poguru.md](Poguru.md)) are ordinary dead
grunts: sneering, rasping, and still a little cowardly. The joke of "Th-that's not natural!" is that a corpse is
saying it.

## Voice

The `SOLDIER` voice in `Tools/generate-voice.mjs`, read hollow and rasping with a dry rattle of breath, then baked
through `ffmpeg` (`GHOST` in that script): pitched down, doubled by a slow chorus, a tomb echo and a faint whisper of
the same line underneath. The army's death cry, death and march sounds (`Tools/generate-sfx.mjs`) are undead too: a
rasping moan cut short, bones clattering down, a tread of bone and rusted armour.

## Keep consistent

- They fall to bones and ash, never blood. Rot and bone, no gore.
- Teal only in their eye-embers (their own souls). Their armour and clan colours are dull and dead.
- They never talk about wanting anything for themselves; they repeat orders.
- Mechanics don't depend on the look: each kind's attacks are as written in `SamuraiKind.cs`.
