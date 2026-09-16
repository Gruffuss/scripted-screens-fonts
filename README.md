# ScriptedScreens Fonts

A client-side **[Stationeers](https://store.steampowered.com/app/544550/Stationeers/)** mod
that makes fonts usable by name in TextMeshPro rich text, including inside
**[ScriptedScreens](https://steamcommunity.com/sharedfiles/filedetails/?id=3666779631)**
labels:

```lua
props = { text = '<font="Barlow Condensed">TANK 3  84%' }
```

It does two independent things:

1. **Registers the game's own font assets**, which TMP otherwise cannot find by name.
2. **Builds font assets from `.ttf`/`.otf` files you drop in a folder**, so you can use a
   font the game does not ship.

No game files are modified and ScriptedScreens is not patched. There is no Lua API — the mod
mutates global TMP state at load, so the only thing that changes is that `<font="X">`
resolves names it previously rejected.

## Using a font

Put the tag inside a label's `text`. It applies from where it appears to the end of the
string, or until `</font>`:

```lua
ui:element({
    id = "readout",
    type = "label",
    rect = { unit = "px", x = 8, y = 8, w = 300, h = 24 },
    props = { text = 'default <font="Barlow Bold">bold Barlow</font> default again' },
    style = { font_size = 16, color = "#22C55E", align = "left" },
})
```

ScriptedScreens enables rich text on every label unless the string contains `<noparse>`, so
nothing needs switching on. The other TMP tags (`<b>`, `<i>`, `<size>`, `<color>`,
`<cspace>`, `<font-weight>`) work alongside it.

**Names are case sensitive.** Take them from `BepInEx/LogOutput.log`, which lists every one
on load:

```
Font available: <font="Barlow Condensed SemiBold"> (212 characters from BarlowCondensed-SemiBold.ttf)
```

Reading a wrong result:

| what you see | what happened |
|---|---|
| the right typeface | the tag resolved |
| the default typeface | the font is not loaded: switched off in the config, or not registered yet |
| a literal `<font="X">` | TMP rejected the tag; the name is wrong (case, or a file name used as a font name) |

`examples/03-name-check.lua` renders a list of names one row each, which makes all three
visible at a glance.

The [ScriptedScreens Html](https://github.com/Gruffuss/scripted-screens-html) mod takes the
same names in CSS `font-family`: it lays pages out with exactly the fonts this mod registers,
so a font you add here is available to pages too.

## Adding your own fonts

Drop `.ttf` or `.otf` files anywhere under the `fonts` folder in the game's save folder:

```
Documents/My Games/Stationeers/fonts/
```

The mod creates it on first launch (the log prints its exact path), and it follows the save
path if you have moved it in the game or LaunchPad settings. Subfolders are scanned, so
organising by project is fine.

**Do not put your fonts in the mod's own folder.** `mods/ScriptedScreensFonts/Assets/fonts`
holds the fonts that ship with the mod, and a Workshop update replaces the mod folder, taking
anything added there with it. A font in your folder with the same name as a bundled one takes
its place.

**The name comes from the font's own metadata** — family, plus style when that is not
Regular:

| file | tag |
|---|---|
| `Barlow-Regular.ttf` | `<font="Barlow">` |
| `Barlow-Bold.ttf` | `<font="Barlow Bold">` |
| `BarlowCondensed-SemiBoldItalic.ttf` | `<font="Barlow Condensed SemiBold Italic">` |

**Every file gets an on/off toggle** in the mod config (LaunchPad's settings UI), grouped
into one section per folder, subfolder and family — `Your fonts: Manrope`,
`Your fonts: GasUi/Manrope` for yours, `Font files: Barlow` for the bundled ones — with the
family taken from the file name up to its first `-`. A disabled file is never
built, so it costs no memory. Use this to keep a whole family in the folder but load only the
weights you use.

**A restart is required** after adding a file or changing a toggle. Atlases are built once,
shortly after the game's TMP resources come up, and there is no rescan.

**Ship the licence.** Most Google Fonts are SIL OFL, which permits bundling provided the
copyright notice and licence travel with the font and it is not sold separately — but check
per family rather than assuming. The font file itself is authoritative: its `name` table
carries the licence in nameID 13. Barlow's `OFL.txt` sits next to the bundled Barlow files.

## Character set

Each font is rendered once into a fixed set:

- printable ASCII
- the Latin-1 supplement (accented European text, `° ± µ ² ³ ¼ ×`)
- 81 baked extras — dashes and curly quotes, maths (`− ≈ ≠ ≤ ≥ ∞ √ ∑ ∏ Δ ∇ π Ω`), arrows,
  geometric shapes, `✓ ✗ ⚠`, block bars for text sparklines, and box drawing

Anything else goes in **`ExtraCharacters`** in
`BepInEx/config/gruffuss.stationeers.scriptedscreens.fonts.cfg`, or LaunchPad's settings UI.

**A glyph the font does not contain is skipped.** No substitution is attempted — that would
mean `<font="Barlow">` silently rendering some other typeface. Barlow, for example, is a text
face: it has the punctuation and the maths but no arrows, shapes or box drawing at all. If
you need an arrow from a font that lacks one, add a font file that has it and switch face for
that character (`<font="Your Font">→</font>`), or draw it another way, for example with
[ScriptedScreens Vector](https://github.com/Gruffuss/scripted-screens-vector). The game's own
symbol faces are no help here: most of them log an error every frame in a label (see Limits).

## Limits

- **One 1024×1024 atlas per face, about 1 MB.** 36 faces is ~36 MB of texture memory. Switch
  off the weights you do not use.
- **Some of the game's own fonts log a Unity error every frame when used in a label** (their
  material has no `_CullMode`). The mod names them in the log when it registers them; avoid
  them in labels.
- **The atlas is static.** It cannot grow at runtime and cannot spill into a second texture.
  If a font's coverage overflows it, the load logs `did not fit` and the remainder is absent;
  the fix is a smaller sampling size.
- **Client-side only.** Skipped entirely in batch mode, since a headless server renders
  nothing.
- Fonts arriving from asset bundles register as they load, so the game's own list fills in
  over the first minutes of a session rather than all at once.

## Examples

In the mod's `examples/` folder, each one pasted as is into a Lua chip in a ScriptedScreens
console:

| file | shows |
|---|---|
| `01-font-tag.lua` | a font by name, switching faces mid-string, other tags on top, UI symbols |
| `02-weights.lua` | every weight of Barlow and Barlow Condensed, and how a file name becomes a font name |
| `03-name-check.lua` | whether a name resolves, with a wrong-case and a file-name row to show the failures |
| `04-readout.lua` | a live readout updated every tick, three weights of one family |

## AI editors (MCP)

With [StationeersLua](https://steamcommunity.com/sharedfiles/filedetails/?id=3659911735)
installed, an AI editor connected to its MCP server can read:

- `stationeers://fonts/index` — a quick start written for AI editors: a label that runs as
  written, the rules that fail silently, a symptom-to-cause table, and a list of every other
  resource
- `stationeers://fonts/available` — every font name registered right now and where it came
  from, including the game fonts to avoid in labels
- this guide one section per resource, the changelog, and the examples (search scope `fonts`)

The same quick start ships as `QUICKSTART.md`. Without StationeersLua nothing changes.

## Requirements

- **BepInEx 5.x** and
  **[StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad)**
- **ScriptedScreens** if you want to use the fonts in consoles — the mod is useful without
  it, but that is the point of it

## Building

Create `Stationeers.VS.User.props` next to the `.csproj` (gitignored):

```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <!-- The folder CONTAINING the Stationeers game directory. -->
    <SteamLibraryDirectory>C:\Program Files (x86)\Steam\steamapps\common</SteamLibraryDirectory>
    <!-- Only if the Documents folder is redirected. Must be the folder that
         actually holds modconfig.xml. -->
    <StationeersDocumentsDirectory>X:\path\to\Documents\My Games\Stationeers</StationeersDocumentsDirectory>
  </PropertyGroup>
</Project>
```

Every deploy `Copy` is `ContinueOnError`, so a wrong `StationeersDocumentsDirectory` fails
silently and presents as the mod not working. If a build succeeds but nothing changes in
game, check that path first.

`dotnet build ScriptedScreensFonts.csproj` deploys to
`My Games\Stationeers\mods\ScriptedScreensFonts\`.

The build publicises `Unity.TextMeshPro` and `UnityEngine.TextCoreFontEngineModule` via
`BepInEx.AssemblyPublicizer.MSBuild`; building a font asset from a file needs several
internal TextCore members. This affects compilation only — the shipped assemblies are
untouched.
