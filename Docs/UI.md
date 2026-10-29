# UI

Part of the [architecture map](../ARCHITECTURE.md). Scripts live under `Assets/Scripts/<System>/`; a bare name like
`Ronin.cs` means the file in its system's folder.

- `GameUI.cs` — HUD (score in one row; stage with the round as a smaller line under it, right-aligned; a small dim
  label beside each number; lives; no high score, so the top centre stays free for the compact boss bar), the stage/round card (`ShowBanner(overline, title, body, accent)`), boss bar, subtitles on a plate with the
  speaker's name (fading in and out, with an optional speaker portrait, used only on the title hill: `ShowSubtitle(…, portrait)` takes a
  `GameUI.SpeakerPortrait`, a close-up plus the point to centre on and the height to draw it, cropped into a framed
  night-dark window at the plate's left, or standing on the plate when it sits `beside` someone), boss intro (a null
  `lineText` makes it a name reveal only, for a talk scene to follow). `ShowSubtitleOver` hangs a line over whoever says
  it in the field (narrow wrap, kept on screen; below their head near the top; an optional `follow` world point moves
  it with the speaker each frame, which `SayLine` passes for Poguru while he is out of the wisp, so his bubble flies with him),
  used through `GameManager.SayLine(…, over)` by Poguru's barks and the tutorial's soldiers, who bob as they speak
  (`TrainingSoldier.Speak`). Talk scenes are `DialogueScene.cs`, built the
  same way from the shared parts below. Builds its pieces in code on the Canvas; `SetHudVisible` hides the HUD behind the
  title menu.
- **Text styles:** `GameUI.Style(text, TextStyle)` gives text its font and a shared outline-and-shadow material
  (`Title` = Cinzel Roman capitals, `GameUI.headerFont`; `Heading`/`Hud`/`Body`/`Hint` = Zen Kaku Gothic New
  Bold, `GameUI.bodyFont`). `GameUI.titleLogo`, when set, replaces the set title on the title menu.
  TTFs are turned into TMP fonts at runtime (`BuildFont`); a missing one falls back to the pixel font. Keep new
  text at 26px or more (1080p reference) and give it a style rather than raw TMP settings.
- **Shared "minimal ink" parts:** every screen except the paper title page is built from the same pieces, so new
  UI should reuse them rather than add framed panels. `GameUI.CreateInk` is a dark dry-brush stroke
  (`ProceduralSprites.InkStroke`) that paints on through its horizontal fill. `GameUI.CreateRule`/`AnimateRule`
  is the divider: a tapered brush line (`ProceduralSprites.BrushLine`) with the menus' diamond at its middle,
  drawn out from the centre. `GameUI.Reveal` makes a title line rise in as its letter spacing closes up. Key
  hints use `MenuUI.KeyCap`. The stage card and the boon screen use all four, and dark `MenuUI` pages use the ink stroke as the
  selection bar (paper pages keep a flat wash); the boss intro keeps its own rule and wash.
- `MenuUI.cs` — one full-screen menu page at a time: title, optional rich-text body, a list of
  `MenuUI.Item.Button` / `MenuUI.Item.Slider` rows, or a row of `MenuUI.Item.Card` panels (a page of only cards: ← → to pick). Keyboard (arrows/WASD, Enter/Space, Esc/Backspace), mouse
  (hover, click, drag sliders) and gamepad (d-pad/stick, South, East/Start); runs on unscaled time. Added to the
  Canvas by `GameManager.Start`.
  `ShowTitle` is the title screen's own light layout (paper page, `GameUI.titleLogo` centred high, a left-aligned
  menu column under it); `Show(..., paper: true)` draws a regular page ink-on-paper, which
  `GameManager.OnPaper` asks for while on the title (so Controls/Settings/Dev Tools match it; from pause they stay
  dark). `ShowFigure` loops a sprite on it (and, given a source pixel, floats `ProceduralSprites.Note` music
  notes up from it), used by `GameManager.ShowTitleRonin` to sit the
  ronin on his row at his in-game pixel scale (`GameManager.titleFluteFrames`, falling back to his idle). The figure
  stays on every paper page opened from the title (`figureWanted`), so START's stand-up can play from the slots page,
  and `LeaveTitle` fades a paper page's words as well as the title's.
- `BoonChoiceUI.cs` — see Boons. For the tutorial: `SlotRect(slot)`, `LiftSlots(layer)`/`DropSlots()` move the ability (and, once shown, the ultimate's medallion)
  slots onto a talk's overlay and back, and `InputLocked` stops a pick while Poguru talks over the screen.
  `GameUI.LivesRow` is lifted the same way for the hearts lesson. `SpriteAnimatorUI.cs` animates UI images in unscaled time.
