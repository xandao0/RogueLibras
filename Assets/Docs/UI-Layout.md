# Where to adjust text, icons and signs

Everything below is authored on **prefabs**, not on the scenes. Edit the prefab
once (double-click it in `Assets/Prefabs/`) and both Battle and MainMenu pick it
up. Editing a scene copy instead creates an *override* that silently stops the
prefab from reaching that scene — if you did that by accident, use the
Inspector's **Overrides ▸ Revert All**.

---

## Card — `Assets/Prefabs/Card.prefab`

The card is 200 × 280 units. All positions are **anchors** (0–1 fractions of the
card), so they hold at any card size. Select a row and use the Rect Transform's
Anchor Min / Anchor Max fields.

| What | Object under `MaskedBody` | Anchors (X, Y) |
|---|---|---|
| Portrait / video | `Portrait` | 0.14–0.87, 0.435–0.825 |
| Card name (over the video) | `NameRow` | 0.15–0.72, 0.428–0.492 |
| Card type ("Carta Comum") | `TypeRow` | 0.14–0.52, 0.330–0.406 |
| Cost + value badges | `BadgeRow` | 0.52–0.90, 0.324–0.412 |
| Description | `Description` | 0.12–0.88, 0.216–0.330 |

**The frame's usable parchment is only Y 0.200 → 0.420.** Below 0.20 the crossed
axes ornament cuts into it and text gets clipped; above 0.42 is the cut-out
window where the video shows through. Keep the three lower rows inside that band.

- `NameRow` and `TypeRow` are `HorizontalLayoutGroup`s holding **[text][SignRow]**.
  The layout group is what keeps a word and its sign from overlapping — don't
  replace it with manual offsets.
- Sign size: `SignRow ▸ Sign Height` (name 19, type 15). `Spacing` is the gap
  between the two glyphs of a two-sign word.
- Badge size: each child of `BadgeRow` is 25 × 25. Add or remove slots by editing
  `CardDisplay ▸ Action Badges` / `Action Badge Labels`.

### Card colours
| Element | Colour |
|---|---|
| Card name | `#3975b3` |
| Card type | `#b1642e` |
| Description, badge numbers | `#491b05` |

The SVGs carry their colour **in the file**, not as a tint — see SignWriting below.

The name has **no plate behind it** — it reads over the video on the strength of
its outline and shadow (`Merriweather CardName`). All card text is Bold.

### Selection glow — `Shine` (CardShine)
Two procedurally generated rounded rings, so the halo follows the card's rounded
silhouette instead of boxing it:

| Field | What it does |
|---|---|
| `Inner Color` / `Inner Strength` | the tight white rim against the card edge |
| `Inner Inset` | how far inside the bloom that rim sits (7) |
| `Color A` / `Color B` / `Color Cycle Speed` | the outer bloom's warm↔cool drift |
| `Min Alpha` / `Max Alpha` / `Pulse Speed` | the breathing |

How far the glow extends past the card is the `Shine` Rect Transform's Left /
Right / Top / Bottom (currently −13 / +13 all round).

---

## Where the cards sit — Battle scene

Two places, both on `GameUI/CurrentHandContainer`:

**Rect Transform ▸ Pos Y** — the height of the whole hand (`-300`). Lower = cards
further off the bottom edge.

**CardFanLayout** — the shape of the fan:

| Field | What it does |
|---|---|
| `Card Spacing` | gap between cards (275). Lower = more overlap |
| `Max Spread Width` | cap on total width, so a big hand compresses instead of running off screen (1340) |
| `Max Fan Angle` | how much the outer cards tilt (26°) |
| `Arc Height` | how far the outer cards drop below the middle one (50) |
| `Normal Scale` | resting card size (1.45) |
| `Hover Scale` / `Hover Y Offset` | size and lift on hover |
| `Selected Scale` / `Selected Y Offset` | size and lift when raised |
| `Follow Speed` | how quickly cards ease to their slot |
| `Greyed Out Alpha` | fade on the cards that aren't raised |

Card size itself is the `Card.prefab` root Rect Transform (200 × 280 — that's the
frame art's 5:7). Changing it rescales every anchor above automatically.

Watch the budget: the enemy needs ~150 px of the view, and the card's text lives
in its bottom fifth, so `Normal Scale` much above 1.45 pushes the description off
the bottom of a 670 px-tall view.

---

## Enemy — `Assets/Prefabs/Enemy.prefab`

Under `EnemyUI`:

| What | Object | Position |
|---|---|---|
| Name | `Name` | Y 382, 34 pt |
| Bar + count group | `HealthBar` | Y 307.8 |
| Bar | `HealthBar/Slider` | X −95, 300 × 40 |
| Count "15/20" | `HealthBar/Health` | X 168, 40 pt |
| Intent icon | `IntentDisplay` | Y 462 |
| Intent number | `IntentAmtDisplay` | X 44, Y 440 |

The whole panel is **anchored to the enemy art**, not to the screen:
`EnemyInfoAnchor` on `EnemyUI`.

- `Vertical Padding` — gap between the top of the sprite and the bar (26).
- `World Scale` — overall size of the panel (0.58).
- `Offset` — nudge if a sprite has empty margins.

Bar colours are on `Slider/Background` (gold rim), `Slider/Track` (dark) and
`Slider/Fill Area/Fill` (red).

---

## SignWriting

**Colour lives in the SVG files**, so the glyphs are already brown/orange and the
`SignRow` tint is white. To recolour, edit the `fill="…"` in the SVGs under
`Assets/SignWriting/` and then re-run both steps:

1. `SignWritingLibrary.asset` ▸ right-click ▸ **Rebuild From SignWriting Folder**
2. Re-blit the TMP atlas (the sprites inside description text come from
   `Assets/TextMesh Pro/Resources/Sprite Assets/SignWriting.asset`)

| Folder | Colour | Used for |
|---|---|---|
| `categories/` | `#b1642e` | Carta Comum (2 glyphs), Combinação |
| `actions/`, `numbers/`, `cards/`, `ui/` | `#491b05` | everything else |

Inline signs in the description use `<sprite name="…" tint=1>`, which makes them
take the surrounding text colour automatically.

### When words turn into signs — `SignProgression.cs`
- `TurnsBeforeFirstSign` (3) — turns played before anything changes.
- `TurnsPerNewSign` (1) — turns between graduations after that.

Each turn, **one** word is picked at random from everything readable in the
current hand and swapped to its sign, so cards convert piece by piece rather than
all at once. Progress is stored in `PlayerPrefs`; `SignProgression.ResetAll()`
puts it back to all-text.

Word → sign mapping is in `CardDisplay.ProcessDescription`:
`Dano/Ataque/Ataca → Dano.svg`, `Cura/Curar → Curar.svg`,
`Defesa/Defende/Defender/Bloqueio → Defesa.svg`.

---

## Editing without the UI in the way

**Tools ▸ RogueLibras ▸**
- *Toggle Battle UI in Scene* (`Ctrl/Cmd + Shift + U`) — hides the hand and HUD
- *Toggle Settings Panel in Scene*
- *Show / Hide All UI in Scene*

These only flip `Canvas.enabled`, so nothing about the layout moves. The settings
panel also starts hidden by default — `SettingsPanel ▸ Backdrop`.

---

## Fonts

| Material | Used by |
|---|---|
| `Merriweather Card` | card type, description, badge numbers |
| `Merriweather CardName` | card name (heavier outline — it sits over video) |
| `Merriweather UI` | HUD, enemy name and count, settings body |
| `Merriweather Button` | button labels on the gold plates |
| `geovano-display …` | the settings window title only |

Each area has its **own material**, so changing one cannot reach into another.
`geovano-display` is a small-caps display face, which is why anything that has to
read in mixed case uses Merriweather instead.

---

## Atmosphere

| Object (Battle scene) | What it does |
|---|---|
| `FogBank` | drifting shader fog, between backdrop and enemy — `SceneFogBank.mat` |
| `SceneGrade` | frame-wide vignette + warm grade — `SceneGrade.mat` |
| `Atmosphere/DustSpecks` | ember motes |
| `Atmosphere/FogBack`, `FogFront` | particle haze |
| `FireLighting` | firelight flicker on the backdrop and the enemy |

`SceneFogBank.mat`: `Density`, `Noise Scale`, `Drift Speed`, `Pool Toward Floor`.
`SceneGrade.mat`: `Vignette`, `Warm Grade`.
