# ScriptedScreens Fonts changelog

Newest first. The workshop page carries only the latest releases; this file has all of them.

## 0.2.0

- Font files: `.ttf` and `.otf` files anywhere under `Assets/fonts` (subfolders included)
  become fonts, named from the font's own metadata (`Barlow-Bold.ttf` is
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
