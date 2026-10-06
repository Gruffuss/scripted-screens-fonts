# ScriptedScreens Fonts changelog

Newest first. The workshop page carries only the latest releases; this file has all of them.

## Unreleased

- `<b>`, `<i>` and `<font-weight>` now draw a family's real faces. Every loaded family links its
  weights to each other, so `<font="Barlow"><b>` is the actual Barlow Bold rather than TextMeshPro
  smearing the Regular, and `<i>` is the drawn italic rather than a slant. Weights the family does
  not ship are left alone. Downloaded families link the same way.
- Fixed: a `FontUrls` link was cut at its first `;` or `,`, so a Google Fonts link asking for two
  weights silently downloaded only the first one -- including the example the setting's own
  description gives. Links are now separated by whitespace only, since both characters occur
  inside a Google Fonts URL (`family=Inter:wght@400;700`, `family=Inter:ital,wght@0,400;1,700`).

- Loaded fonts are kerned: each face's letter-pair spacing is read from the font's OpenType
  GPOS table, so `AV`, `To` and `LT` tuck together the way they do in a browser instead of
  sitting evenly spaced. The `Kerning` setting turns it off. It applies wherever the font is
  used, including a ScriptedScreens Vector `T` label. TextMeshPro only honours the pairs when
  kerning is on in the game's own TMP settings; the log says so once if it is not.
- `stationeers://fonts/available` says whether each font kerns, and with how many pairs. The
  game's own faces carry pair records baked in when they were made, so text on the default font
  is already kerned; the list now shows that instead of it taking a test page to find out.
- Fonts can be downloaded: the `FontUrls` setting takes links to `.ttf`/`.otf` files or Google
  Fonts CSS links. Each is fetched once into `fonts/downloaded` in the save folder, named from
  the font's own metadata, and loads from disk afterwards.
- Other mods can request a font at runtime (`FontApi.RequestFont`), which is how a ScriptedScreens
  Html page's `@font-face` link can load a font. Page downloads are limited to the hosts in
  `PageFontHosts` (default: Google Fonts only), share the download cache, respect switched-off
  fonts, and are capped at 48 faces a session.

## 0.2.0

- Font files: `.ttf` and `.otf` files anywhere under `Documents/My Games/Stationeers/fonts`
  (subfolders included, created on first launch, outside the mod so a Workshop update never
  touches it) become fonts, named from the font's own metadata (`Barlow-Bold.ttf` is
  `<font="Barlow Bold">`). Ships the Barlow and Barlow Condensed families (SIL OFL 1.1).
- Every font file has an on/off switch in the mod config, grouped by subfolder and family. A
  switched-off file is never built and costs no memory.
- Each loaded font covers printable ASCII, Latin-1 and common UI symbols (dashes, curly
  quotes, maths, arrows, shapes, block bars, box drawing), plus the `ExtraCharacters` setting.
  A glyph the font lacks is left out rather than taken from another typeface.
- Documentation and examples are published to the StationeersLua MCP: search scope `fonts`,
  `stationeers://fonts/index` as the quick start, `stationeers://fonts/available` listing the
  names registered right now, the guide one resource per section, the changelog, and every
  chip under `examples/`.
- `examples/`: four chips that run on paste. `QUICKSTART.md` and `CHANGELOG.md` ship in the mod
  folder.
- Game fonts whose material makes a label log a Unity error every frame are named in the log
  and in the MCP list.

## 0.1.0

- The game's own TextMeshPro fonts are registered by name, so `<font="Name">` works in
  ScriptedScreens labels. Fonts that load later in a session are picked up as they arrive.
