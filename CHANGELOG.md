# ScriptedScreens Fonts changelog

Newest first. The workshop page carries only the latest releases; this file has all of them.

## 0.4.0

- Fixed: fonts are always opened from their file now. Opened from memory, the engine could hand
  back a font it already held instead of the one given -- with no characters in it, and under the
  other font's name: a renamed copy of bundled Barlow, the same size, came back as Barlow every
  time, so a downloaded file could be saved as a different font. Downloads are now named from the
  font's own name table, and only TrueType and OpenType files are kept (WOFF and WOFF2 are not). Every font registers as before, with the
  same names, kerning and weights. **While the game runs it keeps every font file it loaded open:
  close the game before replacing or deleting one.** Adding new files is fine.
- A page's font links must be https. Google Fonts serves plain http too, with no upgrade, so an
  http link carried a font file -- which native code parses -- in the clear, where anything on the
  way could replace it. Your own `FontUrls` links may still be http.
- Downloaded fonts now load after the bundled ones, so a name clash goes to the bundled font. They
  sit in your fonts folder, which loads first so that your own files can replace bundled ones, and
  that let a font some page once fetched take a bundled font's name in every later session. Your
  own files still win; every on/off switch is unchanged.
- Fixed: a page's font link could be redirected to a host `PageFontHosts` never allowed. The list
  was checked against the link as written, but the download then followed up to 32 redirects
  wherever they led, so an allowed host could pass the request on and the font's bytes, which
  native code parses, arrived from somewhere else. Page requests now refuse redirects. Google
  Fonts serves its stylesheets and files without them, so nothing that worked stops working; your
  own `FontUrls` links may still redirect.
- Corrected in the docs: a missing bold or italic face is stood in for by TextMeshPro, thickened or
  slanted, not left unstyled (only a `<font-weight=N>` with no face draws unchanged), and
  `<font="Barlow Bold"><i>` draws Barlow Italic, the bold lost. A name nothing is registered under
  prints its tag literally; the default typeface means the face resolved but lacks those
  characters. `FontUrls` links are separated by spaces only, a variable font file gives every named
  instance, and the config sections are named as the settings show them. The README now documents
  `RequestFontWithReason` and `Registrations`.
- Corrected in the docs: the 48-faces-per-session cap on page font requests was described as a
  texture-memory bound of about 1 MB a face. That stopped being true in 0.3.0, when faces became
  dynamic -- an undrawn face costs no texture. The cap bounds how much untrusted font data a page
  can make every player download and parse, which is the reason to keep it. Also corrected:
  atlases are not "built once" (they start empty and fill as characters are drawn), a variable
  font's unused named instances cost nothing, and a missing character is now described as the
  substitution it actually is.
- `FontApi.Registrations` counts the font faces registered this session, by every route: game
  fonts as their bundles load, files, `FontUrls` downloads and page requests. A mod that caches a
  laid-out page can compare it against the number it held at layout time to know whether a redraw
  could change anything -- a face arriving by any route other than its own request callback comes
  with no notification.
- Fixed: a page's font request past the 48-faces-per-session limit downloaded its file in full
  before refusing it, so a page declaring many distinct links, or varying its link, made every
  player fetch files none of them could ever load. The limit now gates the download, and
  downloads still in flight count against it. A cached file is still checked, so a face already
  built this session costs nothing.

## 0.3.0

- **A font file's whole character set is now usable.** Each face starts with an empty atlas and
  builds a glyph the first time something draws it, so the 272-codepoint budget is gone: Barlow
  carries 525 characters and all of them work, where 212 did before. Faces nothing draws cost no
  texture at all, so memory falls rather than rises.
- Corrected in the docs: a glyph the font genuinely lacks **is** substituted by TextMeshPro's own
  global fallback, in another face. The guide previously claimed there was no substitution.

- `FontApi.RequestFontWithReason` tells a caller why a font request produced nothing, so a mod can
  say something useful to a player standing at a console, who cannot read the log: an empty string
  on success, else `download-failed`, `disabled`, `face-limit`, `load-failed` or
  `loader-unavailable`. `RequestFont` is unchanged and keeps its signature.

- `FontApi.RequestFont` now always calls back once it has accepted a link. It waited without a
  limit for the font loader to be ready, so a caller could have been left with a callback that
  never arrived at all; that wait is now bounded and reports nothing loaded instead of hanging.

- New `stationeers://fonts/charset` documentation resource: the exact 272 codepoints every font
  is built from, and, per loaded face, exactly which of them it turns out not to have. A tool can
  now warn that a character will not draw instead of guessing from a prose description -- the
  spread is real, with faces ranging from 212 to 263 of the 272.

- Variable fonts work: a file with named instances builds every one of them as its own face, so
  a single variable `.ttf` gives `<font="Cascadia Code Light">` through `<font="Cascadia Code
  Bold">` and links their weights like any other family. Each instance carries its own atlas, so
  a font with many instances costs proportionally more memory; switch the file off in the config
  if that is not wanted. (Fonts downloaded from Google Fonts are static files already, one per
  weight, so this only concerns files put in the fonts folder by hand.)
- The font rescan now backs off once a session settles -- each quiet pass doubles the wait, up to
  about five minutes, and drops back the moment a font appears -- instead of walking every loaded
  object every 20 seconds for the whole session. Fonts that arrive late are still picked up.
- Each registration logs how many fonts arrived and how long after startup, which is the first
  measurement of how late game fonts really turn up.

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
