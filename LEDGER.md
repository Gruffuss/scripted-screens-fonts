# Ledger

Living record of this mod's work. Newest state first; `- [ ]` open, `- [x]` done.

## Open

- [ ] `FontUrls` downloads and `FontApi.RequestFont` have never been exercised in game: the
      setting is empty, so no launch has fetched anything. Check for `Downloaded ...` in the log,
      the files plus `sources.txt` in the download cache, and no refetch on the next launch.
- [ ] Real weights via TMP's `fontWeightTable`: fill a Regular asset's table with its siblings so
      `<b>`, `<i>` and `<font-weight>` pick real faces instead of synthesising them.
- [ ] Variable fonts load only their default instance; named instances through the high bits of
      `faceIndex` are untested.
- [ ] Heartbeat stop condition from an expected font set (game fonts currently register forever).
- [ ] Entry-wording reorder is built and deployed but not yet loaded by a running game.

## Done

- [x] Every font's kerning pair count is published in the MCP font list, game faces included, so
      "does this face kern" needs no test page. Confirmed in game: 62 entries, 43 from files and
      19 from the game, each with a count.
- [x] Fonts built from files are kerned, from the font's OpenType GPOS table. Seen in game: three
      faces each landed within 3 px of the width predicted from their own GPOS values, and 15 px
      short of the unkerned width.
- [x] Per-file on/off switches, subfolder scanning, downloads, the runtime font API with a host
      allowlist, the MCP documentation scope and the shipped examples.

## Do not re-derive

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
