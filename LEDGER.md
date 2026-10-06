# Ledger

Living record of this mod's work. Newest state first; `- [ ]` open, `- [x]` done.

## Waiting on a game start

- [x] Catalogue entry wording reordered so the `_CullMode` warning comes last (`4cd2f89`), built
      and deployed. Not yet loaded: it takes effect at the next game start, like any build here.
      Nothing to do, and nothing depends on it -- it changes one documentation string.

## Backlog

- [ ] `RequestFontWithReason` is built and deployed but **not seen in game**. How each token can
      actually be provoked, since they are not equally reachable:
      - `download-failed` -- an allowed host with a path that 404s. Genuine.
      - `disabled` -- switch a downloaded file off in the config, then request its link. Genuine.
      - `load-failed` -- an allowed host serving a file that is not a font. Genuine.
      - `face-limit` -- needs 48 page faces in one session, so it is reachable only by lowering
        the cap in a temporary build: that checks the arithmetic, not the real limit.
      - `loader-unavailable` -- **cannot occur naturally**: it needs the shader to stay missing
        for 60 s, which does not happen. Only a temporary build holding that condition can
        exercise it. Say so when reporting it, or the claim is stronger than the evidence.


## Done

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
