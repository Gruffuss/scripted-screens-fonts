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
mutates global TMP state at load, so the main thing that changes is that `<font="X">` resolves
names it previously rejected.

It does patch TextMeshPro, in one narrow place: two Harmony patches, one on TextMeshPro's glyph
growth and one on Unity's font engine, let a font built from a file rasterise a glyph on demand,
which TMP otherwise only does for fonts imported in the editor. The engine patch is inert unless
one of this mod's own faces is growing, so the game's fonts take the untouched path.

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
nothing needs switching on. The other TMP tags (`<size>`, `<color>`, `<cspace>`) work alongside
it, and **`<b>`, `<i>` and `<font-weight=N>` pick the family's real faces** when it ships them —
`<font="Barlow"><b>text</b>` draws exactly the same as `<font="Barlow Bold">text`, rather than
TextMeshPro thickening or slanting the regular. Where the family has no such face, TextMeshPro's
own stand-in applies: `<b>` thickens the face in use and `<i>` slants it, while a
`<font-weight=N>` with no matching face draws the face unchanged. Combine through the family, not
a weight's name: `<font="Barlow"><b><i>` draws Barlow Bold Italic, but `<font="Barlow Bold"><i>`
draws Barlow Italic -- naming the bold face does not make the italic bold. Text is also kerned,
from the font's own pair data.

**Names are case sensitive.** Take them from `BepInEx/LogOutput.log`, which lists every one
on load:

```
Font available: <font="Barlow Condensed SemiBold"> (every character in the file, built as it is first drawn, 9288 kerning pairs from BarlowCondensed-SemiBold.ttf)
```

Reading a wrong result:

| what you see | what happened |
|---|---|
| the right typeface | the tag resolved |
| a literal `<font="X">` | nothing is registered under that name, so TMP rejected the tag: wrong case, a file name used as a font name, a file switched off in the config, or a font not registered yet |
| the default typeface | the name resolved, but that face has none of these characters, so TMP drew each from its own fallback (a symbol font asked for letters, for example) |

`examples/03-name-check.lua` renders a list of names one row each, which makes all three
visible at a glance.

[ScriptedScreens Vector](https://github.com/Gruffuss/scripted-screens-vector) takes the same
names in a `T` label's `font=` and in `<font>` tags inside its text, and the
[ScriptedScreens Html](https://github.com/Gruffuss/scripted-screens-html) mod takes them in CSS
`font-family`, so a font you add here is available to their pages too.

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
its place. Downloaded fonts do not: anything in `fonts/downloaded` loads after the bundled fonts,
so a font a page once fetched can never replace one the mod ships.

**The name comes from the font's own metadata** — family, plus style when that is not
Regular:

| file | tag |
|---|---|
| `Barlow-Regular.ttf` | `<font="Barlow">` |
| `Barlow-Bold.ttf` | `<font="Barlow Bold">` |
| `BarlowCondensed-SemiBoldItalic.ttf` | `<font="Barlow Condensed SemiBold Italic">` |

**Every file gets an on/off toggle** in the mod config (LaunchPad's settings UI), grouped
into one section per folder, subfolder and family — `Your fonts (restart to apply): Manrope`,
`Your fonts (restart to apply): GasUi/Manrope` for yours,
`Included fonts (restart to apply): Barlow` for the bundled ones — with the family taken from the
file name up to its first `-`. A disabled file is never
built, so it costs no memory. Use this to keep a whole family in the folder but load only the
weights you use.

**Close the game before replacing or deleting a font file**: while it runs, the game keeps every
font file it loaded open, so Windows refuses. Adding a new file is fine.

**A restart is required** after adding a file or changing a toggle. Faces are built once,
shortly after the game's TMP resources come up, and there is no rescan. Their atlases start
empty and fill as characters are drawn, so building a face is not what costs.

**Ship the licence.** Most Google Fonts are SIL OFL, which permits bundling provided the
copyright notice and licence travel with the font and it is not sold separately — but check
per family rather than assuming. The font file itself is authoritative: its `name` table
carries the licence in nameID 13. Barlow's `OFL.txt` sits next to the bundled Barlow files.

## Downloading fonts

Instead of copying files, list links in **`FontUrls`** in the mod settings, separated by spaces
-- not commas, which a Google Fonts link contains itself. Two kinds work:

- a direct link to a `.ttf` or `.otf` file
- a Google Fonts stylesheet link, which brings every weight it names:
  `https://fonts.googleapis.com/css2?family=Manrope:wght@400;700`

Each link is downloaded **once**, into `Documents/My Games/Stationeers/fonts/downloaded`, named
from the font itself (`Manrope-Bold.ttf`). From then on it loads from there like any of your
fonts and needs no network. A font downloaded this launch is usable this launch; its on/off
switch appears after the next restart. Removing a link does not delete its files; delete them
from that folder.

Variable fonts work through Google: it sends one ordinary file per weight you list, and a range
such as `wght@100..900` brings one file for every weight in it. A variable `.ttf` linked directly,
or dropped in your folder, gives each of its named instances under its own name
(`<font="Cascadia Code SemiBold">`). WOFF and WOFF2 files are not read.

### Fonts requested by a page

Other mods can ask for a font at runtime — ScriptedScreens Vector does it for a scene's `FONT`
link, and ScriptedScreens Html for a page's `@font-face { src: url(...) }`. A page is written by whoever built the console, and in
multiplayer its source reaches every player, while a font file is read by native code. So a
page may only download from the hosts in **`PageFontHosts`**, which defaults to Google Fonts
alone (`fonts.googleapis.com fonts.gstatic.com`): Google serves only its own curated files,
where a general CDN serves anything anyone uploads. A stylesheet's font links must pass the
same check. Page links must be **https**, and a redirect is refused rather than followed: the host
list can only vouch for where the bytes actually come from. Empty refuses every page download; the
setting applies at once.

Page fonts use the same `fonts/downloaded` cache, so each downloads once. A font you have
switched off stays off, and a session builds at most 48 page font faces, checked before anything
is downloaded. The cap is on how many faces pages may bring in, not on memory: a face costs only
the glyphs something actually draws.

For mod authors, the call, by reflection so neither mod needs the other to build:

```csharp
var api = Type.GetType("ScriptedScreensFonts.FontApi, ScriptedScreensFonts");
api?.GetMethod("RequestFont")?.Invoke(null, new object[] { link, (Action<string[]>)(names => { /* "Manrope", "Manrope Bold" */ }) });
```

It returns `false` when the link is refused (the reason is logged) and otherwise calls back
once, on the main thread, with the names the fonts are available under.

`RequestFontWithReason(link, Action<string[], string>)` is the same call, and also tells the
callback why it got nothing: an empty string on success, else `download-failed`, `disabled`,
`face-limit`, `load-failed` or `loader-unavailable`. Look it up by its own name; it is never an
overload of `RequestFont`, so `GetMethod("RequestFont")` stays unambiguous.

`FontApi.Registrations`, a property, counts the font faces registered this session by every
route, the game's own included, and only ever increases. A mod that keeps a laid-out page can
compare it with the value it held at layout time to learn that a font has arrived since -- a
label that was drawn before its font existed does not redraw by itself.

## Character set

**Every character the font file contains is available.** Each glyph is rendered the first time
something draws it, so there is no set to stay inside: Barlow carries 525 characters and all of
them work, Latin Extended, Greek and currency included.

A glyph the font genuinely does not contain is a different matter. Barlow, for example, is a
text face with no arrows, geometric shapes or box drawing anywhere in it. **TextMeshPro
substitutes those from its own global fallback**, so the character appears, but in another face
and often at another weight — which is rarely what you wanted in the middle of a line. This mod
adds no fallback of its own and cannot switch TMP's off. If a character matters, use a font file
that has it (`<font="Your Font">→</font>`), or draw it another way, for example with
[ScriptedScreens Vector](https://github.com/Gruffuss/scripted-screens-vector). The game's own
symbol faces are no help: most of them log an error every frame in a label (see Limits).

`stationeers://fonts/charset` over the MCP server answers this exactly — the codepoints each
loaded face does and does not have, rather than a description you have to interpret.

## Limits

- **A face costs nothing until something draws it.** Its atlas starts empty and grows to
  1024×1024 on the first glyph, spilling into further textures if that fills. Loading many
  weights you never use is therefore cheap; drawing with them is what costs.
- **The first frame that draws a character a face has not built yet pays to rasterise it.**
  That is once per character per face per session, and nothing on a redraw once warm.
- **Some of the game's own fonts log a Unity error every frame when used in a label** (their
  material has no `_CullMode`). The mod names them in the log when it registers them; avoid
  them in labels.
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

The same quick start ships as `QUICKSTART.md`. The MCP server is off by default in
StationeersLua's own settings; the fonts work the same either way.

## Requirements

- **BepInEx 5.x** and
  **[StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad)**
- **[ScriptedScreens](https://steamcommunity.com/sharedfiles/filedetails/?id=3666779631)**
  — the labels the fonts are used in
- **[StationeersLua](https://steamcommunity.com/sharedfiles/filedetails/?id=3659911735)**
  — the Lua chips that drive ScriptedScreens (ScriptedScreens requires it)

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

`dotnet build ScriptedScreensFonts.csproj -c Release` deploys to
`My Games\Stationeers\mods\ScriptedScreensFonts\`.

The build publicises `Unity.TextMeshPro` and `UnityEngine.TextCoreFontEngineModule` via
`BepInEx.AssemblyPublicizer.MSBuild`; building a font asset from a file needs several
internal TextCore members. This affects compilation only — the shipped assemblies are
untouched.
