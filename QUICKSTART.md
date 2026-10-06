# ScriptedScreens Fonts: quick start

Written for an editor or an AI styling console text. Everything here runs as written.

## What it is

A client-side mod that makes fonts usable by name in TextMeshPro rich text, including every
ScriptedScreens `label`. It registers the game's own font assets, and builds fonts from
`.ttf`/`.otf` files: the ones it ships (Barlow, Barlow Condensed) and the player's own in
`Documents/My Games/Stationeers/fonts`. There is no Lua API: the only change is
that `<font="Name">` inside a label's text now resolves.

The ScriptedScreens Html mod uses the same names in CSS `font-family`.

## Workflow

1. Get the exact names: read `stationeers://fonts/available`, which lists every font
   registered right now and where it came from. The same names are in
   `BepInEx/LogOutput.log` on the `Font available:` lines.
2. Put `<font="Name">` inside the label's `props.text`. It applies until `</font>` or the end
   of the string.
3. Paste the script into a Lua chip in a ScriptedScreens console, or from an MCP client write
   it with StationeersLua's `set_chip_code` (chip ids come from `list_chips`).
4. Look: `capture_scripted_screen` with the chip's ref returns a PNG of the console.
5. Errors: `get_chip_errors` for Lua.

## A label, as written

```lua
local ui = ss.ui.surface("main")
ss.ui.activate("main")
local size = ui:size()
local W, H = 460, 460
if size then W, H = size.w, size.h end
ui:clear()

ui:element({ id = "bg", type = "panel", rect = { unit = "px", x = 0, y = 0, w = W, h = H }, style = { bg = "#0B1622" } })

local value = ui:element({
    id = "press",
    type = "label",
    rect = { unit = "px", x = 16, y = 16, w = W - 32, h = 48 },
    props = { text = "" },
    style = { font_size = 34, color = "#E4F1F7", align = "left" },
})
ui:commit()

function tick(dt)
    local kpa = 101.3  -- replace with a device read
    value:set_props({ text = '<font="Barlow Condensed">Pressure </font><font="Barlow Bold">'
        .. string.format("%.1f", kpa) .. '</font><font="Barlow Light"> kPa</font>' })
    ui:commit()
end
```

## Rules that fail silently

- **Names are case sensitive.** `barlow` is not `Barlow`.
- **A file's font name is not its file name.** `Barlow-Regular.ttf` is `Barlow`;
  `BarlowCondensed-SemiBold.ttf` is `Barlow Condensed SemiBold`. The name is the font's own
  family, plus its style when that is not Regular.
- **Each loaded font has a fixed character set:** printable ASCII, Latin-1, and common UI
  symbols (dashes, curly quotes, `− ≈ ≠ ≤ ≥ ∞ √ Δ π Ω`, arrows, shapes, block bars, box
  drawing), plus anything in the `ExtraCharacters` setting. A character outside that set, or
  one the font file does not contain, does not draw. Barlow has no arrows, shapes or box
  drawing. Subscript digits are not in the set: write `CO2`.
- **No substitution.** A missing glyph is not taken from another font. Switch face for that
  character to a font file that has it, or draw it another way. Do not reach for the game's
  symbol faces (`noto-punc` and most others): `stationeers://fonts/available` marks them as
  logging an error every frame in a label.
- **Loaded fonts are kerned** (from the font's GPOS table), so a line of text draws slightly
  narrower than the sum of its characters' widths. Anything measuring text by adding up glyph
  advances will over-estimate. The `Kerning` setting turns it off. Most of the game's own faces
  kern too, from pairs baked into them when they were made; `stationeers://fonts/available`
  gives the pair count for every font, so a face that does not kern is visible there.
- **`<b>`, `<i>` and `<font-weight=N>` pick the family's real faces** when it ships them, so
  `<font="Barlow"><b>text</b>` equals `<font="Barlow Bold">text` exactly. A weight the family does
  not have falls back to the face in use, unstyled.
- **Adding a font file or toggling one needs a game restart.** Fonts are built once.
- **Fonts can also be downloaded:** links in the `FontUrls` setting (a `.ttf`/`.otf` file, or a
  Google Fonts CSS link such as `https://fonts.googleapis.com/css2?family=Manrope:wght@400;700`)
  are fetched once into `fonts/downloaded` and named from the font (`Manrope-Bold` is
  `<font="Manrope Bold">`). WOFF2 is not read. Separate several links with **spaces, not commas**:
  a Google Fonts URL contains commas and semicolons of its own. A page (ScriptedScreens Html `@font-face`) may
  download only from the hosts in `PageFontHosts`, Google Fonts by default.
- **Own fonts go in `Documents/My Games/Stationeers/fonts`, never in the mod folder**, which a
  Workshop update replaces.
- **Some of the game's own fonts log a Unity error every frame in a label** (their material
  lacks `_CullMode`). `stationeers://fonts/available` marks them; avoid them in labels.
- **`<noparse>` anywhere in a label's text turns rich text off for the whole label.**
- **Game fonts keep registering for the first minutes of a session.** A name missing from
  the list early may appear later.
- Every font file can be switched off in the mod config (`Your fonts: <family>` and
  `Font files: <family>` sections); a switched-off font does not resolve.

## Symptom to cause

| what the console shows | cause |
|---|---|
| the text in the right face | the name resolved |
| the literal `<font="X">` | the name is wrong: case, or a file name used as a font name |
| the text in the default face | the font is not loaded: switched off, or not registered yet |
| some characters missing | the font has no glyph for them, or they are outside the character set |
| Unity `_CullMode` errors in the log | a flagged game font is used in a label |

## Checklist

- [ ] every name copied from `stationeers://fonts/available`, case included
- [ ] tags inside `props.text`, nothing in `style` selects a font
- [ ] characters limited to the set above, or listed in `ExtraCharacters`
- [ ] no flagged game font in a label
- [ ] checked with `capture_scripted_screen`
