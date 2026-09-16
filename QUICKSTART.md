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
  character (`<font="noto-punc">→</font>`) or draw it another way.
- **Adding a font file or toggling one needs a game restart.** Fonts are built once.
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
