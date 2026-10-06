# Ledger

Living record of this mod's work. Newest state first; `- [ ]` open, `- [x]` done.

## Waiting on a game start

- [x] Catalogue entry wording reordered so the `_CullMode` warning comes last (`4cd2f89`), built
      and deployed. Not yet loaded: it takes effect at the next game start, like any build here.
      Nothing to do, and nothing depends on it -- it changes one documentation string.

## Backlog

Empty. Nothing in this mod is started-and-unfinished.

## Done

- [x] Removed the `ExtraCharacters` setting. It existed to widen a fixed character set that no
      longer exists; every character is built on demand. The fallback path keeps its own built-in
      set and needs no configuring.

- [x] Settings UI tidied: the per-file description cut to one line (it was repeated under all
      three dozen files), the `ExtraCharacters` text corrected since it no longer gates anything,
      and the file sections renamed so the settings sort first. **Not seen** -- the UI cannot be
      captured from here; next launch shows it.

- [x] Released as 0.3.0: README, Steam description and version brought up to date with on-demand
      characters, real weights, kerning, downloads and variable fonts. Merged to `main`.

- [x] `RequestFontWithReason` seen on a console. `download-failed` confirmed by two routes (a 404
      on an allowed host, and an allowed host serving a non-font), `disabled` confirmed by
      switching a cached font off in the config and asking for its link, and a disallowed host
      confirmed to return false **with no callback at all**. The `disabled` run also verified the
      config switch itself: the font did not register that session.

- [x] **Complete character coverage.** Faces are built as TMP dynamic assets with an empty atlas
      and grow on demand, so every character a font file holds can be drawn. Seen in game:
      Latin Extended-A and B, currency and Greek all drew from Barlow, which could only ever
      show 212 of its 525 characters before. The whole blocker was one line, redirected with two
      Harmony prefixes and no IL rewriting.
- [x] Corrected a false claim in the shipped guide: a glyph the font lacks **is** substituted,
      by TMP's global fallback. Barlow has no arrow, circle or box-drawing glyph and a label
      asking for them drew them anyway, in another face.

- [x] Measured how late game fonts arrive, which was pure folklore before ("up to an hour", never
      measured, now removed from the comments). Two separate sessions agree: the last arrival is
      at 80-82 s, after roughly 48-54 fonts at 16-18 s and two groups of 8 after that, then
      nothing for the rest of the session. See the known-unknown below for what this does not
      cover.

- [x] `stationeers://fonts/charset` publishes the exact character set and each face's gaps, so a
      page builder can warn precisely rather than from prose. Seen in game: 45 faces listed,
      Barlow's 60 gaps being exactly the arrows, shapes, block elements and box drawing.

- [x] Variable fonts build every named instance as its own face, named from its own metadata and
      weight-linked with its siblings. Seen in game: one file gave six faces, five of which fell
      into weight slots (SemiLight has no TextMeshPro slot, so it is a name only).
- [x] `FontApi.RequestFont` exercised in game through reflection, as another mod calls it: the
      signature resolves, an allowed host is accepted and calls back with the registered name,
      and a disallowed host is refused before anything is fetched.
- [x] The rescan backs off when a session is quiet (doubling to about five minutes, reset by any
      arrival) instead of walking every loaded object every 20 s for ever. Chosen over the stop
      condition this ledger used to ask for: there is no font set to expect, so any threshold
      that ends the scan can go blind.

- [x] `<b>`, `<i>` and `<font-weight>` draw a family's real faces: every family links its weights
      to each other. Seen in game -- six pairs, each string drawn once by face name and once
      tagged on Regular, matched to identical pixel widths, a downloaded family included.
- [x] `FontUrls` downloads confirmed in game: a Google Fonts link fetched both requested weights
      into the cache with a `sources.txt` entry, and the next launch reused them without
      refetching. The links are whitespace-separated -- `;` and `,` were separators and cut a
      Google Fonts URL in half, so only its first weight arrived.
- [x] Every font's kerning pair count is published in the MCP font list, game faces included, so
      "does this face kern" needs no test page. Confirmed in game: 62 entries, 43 from files and
      19 from the game, each with a count.
- [x] Fonts built from files are kerned, from the font's OpenType GPOS table. Seen in game: three
      faces each landed within 3 px of the width predicted from their own GPOS values, and 15 px
      short of the unkerned width.
- [x] Per-file on/off switches, subfolder scanning, downloads, the runtime font API with a host
      allowlist, the MCP documentation scope and the shipped examples.

## Do not re-derive

- **The static fallback exists for a TextMeshPro change, not a Harmony failure.** Harmony cannot
  be unavailable: BepInEx is Harmony, so a plugin that is running at all has it. What can fail is
  `AccessTools.Method(typeof(TMP_FontAsset), "TryAddCharacterInternal")` returning null after a
  game update moves those internals -- which does happen, as `GetFontAssetForWeight` in this very
  build is TMP dead code left behind by exactly that drift. Kept deliberately (owner's call,
  2026-10-06): without it a failed patch leaves every face Dynamic with an empty atlas and no way
  to load the face, so no glyph ever builds and every label falls through to a substitute face.

- **LaunchPad sorts settings sections alphabetically** (`a.Category.CompareTo(b.Category)`) and
  renders each with `ImGuiTreeNodeFlags.DefaultOpen` hardcoded. So binding order does not affect
  display order, and **a section cannot be made to start collapsed** from a mod. Ordering is
  controlled only by choosing section names: `"Font files: X"` sorted before `"Fonts"` because
  the space beats the `s`.

- **A page font request has exactly three outcomes**, measured on a console, not five: refused
  (returns false, no callback at all), `download-failed`, or `disabled`. Build reports on three.
  - `load-failed` is **unreachable by URL**: `FontDownloader` validates a file with
    `LoadFontFace` before it ever becomes a path, so a non-font is rejected at the download step
    and reports `download-failed`. It would need an already-cached file to have gone corrupt.
  - `face-limit` needs 48 page faces in one session and `loader-unavailable` needs the shader to
    stay missing for 60 s, so neither occurs in ordinary play. Both exist to stop a caller being
    left with no answer, not because the situations are common.

- **TMP's dynamic growth needs exactly one thing this mod cannot give it**: a Unity `Font` to
  re-open the face with. Everything after that line in `TryAddCharacterInternal` is generic.
  A prefix noting which asset is growing, plus a prefix on `FontEngine.LoadFontFace(Font, int)`
  that loads the file's bytes instead, is the whole fix -- no IL rewriting, so it survives
  anything but a change to those two signatures.
- **The atlas must stay readable** for growth; `makeNoLongerReadable: true` would break it. That
  earlier memory idea is dead, and starting the atlas empty saves far more anyway.
- **An empty `Texture2D(0, 0)` is the supported starting point**, not a trick: it is exactly what
  `TMP_FontAsset.CreateFontAsset` does for a dynamic asset, and TMP resizes it on first glyph.

- **A font asset cannot grow here without a Harmony patch, and that is a choice, not a law.**
  Both routes gate on the same call: `TryAddCharacterInternal` and the public `TryAddCharacters`
  each begin with `FontEngine.LoadFontFace(m_SourceFontFile, ...)` and bail when it fails, and a
  hand-built asset has no source `Font`. A prefix cannot merely pre-load the face, because the
  original re-loads and fails; it needs a transpiler over that call, or a prefix that adds the
  glyph itself -- which is the code this mod already runs at load time. The mod patches nothing
  by design; do not restate that as "impossible".
- **A Unity `Font` cannot be built from bytes or a path.** The whole surface is `Font()`,
  `Font(string name)` (an empty font with a name, it reads no file) and
  `CreateDynamicFontFromOSFont(name|names, size)` (an installed family). This one really is a
  dead end, and is why faces are built from raw bytes through `FontEngine` instead.
- **Atlas memory is width x height bytes in Alpha8, twice over** while the texture stays
  readable: 1 MB GPU + 1 MB CPU per face at 1024, so 4096 would be ~32 MB a face, not 16.
- **One atlas per face with everything at `atlasIndex 0` is this mod's own implementation**, not
  a TextureMeshPro limit: `SetupNewAtlasTexture` exists and TMP spills across atlases. Lifting
  the character budget means using it, which is work, not research.

- **Requests are deduplicated by absolute URI.** Fifteen consoles declaring one link produce one
  download; every caller's callback is queued on the same pending request and all of them fire.
  A caller needs no dedup of its own. A request that produced nothing is forgotten, so a later
  request retries rather than replaying the failure.

- **`FontApi.RequestFont`'s callback is guaranteed once the call returns true**, on the main
  thread, with an empty array meaning nothing loaded. The delivery sits in a `finally`, so every
  failure path reaches it. Do not add a `RequestFont` *overload*: both consumers resolve it with
  `GetMethod("RequestFont")` by name alone, which throws `AmbiguousMatchException` as soon as a
  second one exists. A richer call needs a different name.

- **The 80 s figure is for a session where nobody moves.** Signage faces load with the prefabs
  that use them, so a player walking somewhere new can still bring one in much later. That case
  needs a normal play session to measure and has never been observed, only reasoned about; the
  rescan therefore backs off rather than stopping, and no code should assume the font list is
  final. Every registration logs its arrival time, so any ordinary session settles it for free.

- **Google Fonts never serves a variable font** to this downloader: it answers with static
  instances, one file per weight, even for a variable-only family. Variable files only arrive
  when a player puts one in the fonts folder.
- **FreeType selects a named instance through the high half of the face index** (1-based), which
  is what makes one variable file into several faces.

- **Weight slots come from `TMP_FontAssetUtilities`** (Thin 1 .. Regular 4 .. Black 9), which is
  what actually reads `fontWeightTable`. `TMP_Text.GetFontAssetForWeight` indexes `weight / 100`
  and is dead code in this version -- following it gives the wrong slot.
- **A Google Fonts URL contains `;` and `,`**, so neither can separate links in a setting.

- The engine's **per-lookup GPOS reader returns raw design units**, unlike the legacy kern-table
  reader which returns pixels. Values must be scaled by `SamplingPointSize / head.unitsPerEm` or
  text collapses into itself. Nothing in the API says this, and this Unity's own TextCore never
  calls that overload, so there is no in-engine precedent to copy.
- **`FaceInfo` does not expose the em size**, and it is not always 1000: 2000 and 2048 both occur
  in fonts used here. It is parsed out of the font file's `head` table.
- **TextMeshPro has no kerning rich-text tag.** `enableKerning` is per text component and the tag
  parser never reaches it, so a font name cannot carry it. A per-label opt-out has to come from
  whichever mod creates the label.
- **The game's own faces already kern**, from pairs baked into the assets when they were made:
  nine of nineteen carry them, and the default face measures about 5% narrower kerned than not.
  Kerning was never introduced to the game by this mod, only to the faces it builds.
- **`TMP_FontAsset.CreateFontAsset` cannot load a file** here, and a font asset back-references
  its characters (`character.textAsset = this`), so two assets cannot share one character table.
