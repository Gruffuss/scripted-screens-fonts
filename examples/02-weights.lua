-- 02-weights.lua -- every weight of a family, by name. Paste into a Lua chip in a
-- ScriptedScreens console.
-- Shows: how a font file's name becomes a tag. The name is the font's own family, plus its
-- style when that is not Regular: Barlow-Light.ttf is "Barlow Light", Barlow-Regular.ttf is
-- just "Barlow". A weight switched off in the mod config does not load, and its row falls
-- back to the default face.

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
    style = { bg = "#0B1622" },
})

local WEIGHTS = { "Thin", "ExtraLight", "Light", "", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" }

local function column(id, family, x)
    local row = (H - 24) / #WEIGHTS
    for i, weight in ipairs(WEIGHTS) do
        local name = weight == "" and family or (family .. " " .. weight)
        ui:element({
            id = id .. i,
            type = "label",
            rect = { unit = "px", x = x, y = math.floor(12 + (i - 1) * row), w = W / 2 - 16, h = math.floor(row) },
            props = { text = '<font="' .. name .. '">' .. (weight == "" and "Regular" or weight) .. ' 0123</font>' },
            style = { font_size = math.floor(math.min(28 * W / 460, row - 6)), color = "#E4F1F7", align = "left" },
        })
    end
end

column("wide", "Barlow", 12)
column("narrow", "Barlow Condensed", W / 2 + 4)

ui:commit()
