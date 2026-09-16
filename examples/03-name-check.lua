-- 03-name-check.lua -- is this font name right? Paste into a Lua chip in a ScriptedScreens
-- console and edit NAMES.
-- Shows: the three ways a <font> tag can come out, one row per name:
--   the row is in its own typeface      -> the name resolved
--   the row prints the tag literally    -> TextMeshPro rejected the name (usually case or spelling)
--   the row is in the default typeface  -> the name resolved to nothing usable
-- Names are case sensitive. The mod writes every available name to BepInEx/LogOutput.log
-- ("Font available:" lines).

local NAMES = {
    "Barlow",
    "Barlow Condensed",
    "Barlow SemiBold Italic",
    "barlow",            -- wrong case: prints its tag
    "Barlow-Regular",    -- the file name, not the font name: prints its tag
}

local ui = ss.ui.surface("main")
ss.ui.activate("main")

local size = ui:size()
local W, H = 460, 460
if size then W, H = size.w, size.h end

ui:clear()

ui:element({
    id = "bg",
    type = "panel",
    rect = { unit = "px", x = 0, y = 0, w = W, h = H },
    style = { bg = "#0F172A" },
})

ui:element({
    id = "control",
    type = "label",
    rect = { unit = "px", x = 12, y = 8, w = W - 24, h = 24 },
    props = { text = "Default face, for comparison: abcdefg 0123" },
    style = { font_size = 16, color = "#94A3B8", align = "left" },
})

-- One row per name, wide enough that a rejected name printing its whole tag does not wrap
local row = math.min(48, (H - 44) / #NAMES)
for i, name in ipairs(NAMES) do
    ui:element({
        id = "name" .. i,
        type = "label",
        rect = { unit = "px", x = 12, y = math.floor(40 + (i - 1) * row), w = W - 24, h = math.floor(row) },
        props = { text = '<font="' .. name .. '">' .. name .. ' 0123' },
        style = { font_size = math.min(22, math.floor(row) - 8), color = "#22C55E", align = "left" },
    })
end

ui:commit()
