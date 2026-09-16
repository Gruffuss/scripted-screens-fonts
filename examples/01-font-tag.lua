-- 01-font-tag.lua -- a label in a font the game does not ship. Paste into a Lua chip in a
-- ScriptedScreens console.
-- Shows: <font="Name"> inside a label's text, switching faces mid-string, and the other
-- TextMeshPro tags (<size>, <color>, <cspace>, <b>) working alongside it. Rich text is on in
-- every ScriptedScreens label, so nothing needs enabling.

local ui = ss.ui.surface("main")
ss.ui.activate("main")

local size = ui:size()
local W, H = 460, 460
if size then W, H = size.w, size.h end
local S = W / 460  -- consoles report different canvas sizes; lay out for 460 and scale

ui:clear()

ui:element({
    id = "bg",
    type = "panel",
    rect = { unit = "px", x = 0, y = 0, w = W, h = H },
    style = { bg = "#0B1622" },
})

local function label(id, y, h, text, fsize, color)
    ui:element({
        id = id,
        type = "label",
        rect = { unit = "px", x = 16 * S, y = y * S, w = W - 32 * S, h = h * S },
        props = { text = text },
        style = { font_size = math.floor(fsize * S), color = color, align = "left" },
    })
end

-- The whole label in one face
label("title", 16, 44, '<font="Barlow Condensed SemiBold">HAB CORE</font>', 34, "#38BDF8")

-- The default face for comparison
label("plain", 70, 26, "Default face: Pressure 101.3 kPa", 18, "#7A93A6")

-- The same text in Barlow
label("barlow", 100, 26, '<font="Barlow">Barlow: Pressure 101.3 kPa</font>', 18, "#E4F1F7")

-- Faces mixed in one string: the tag applies until </font>
label("mixed", 140, 34,
    '<font="Barlow">Pressure </font><font="Barlow Bold">101.3</font><font="Barlow Light"> kPa</font>',
    24, "#E4F1F7")

-- Other tags on top of a face
label("tags", 184, 34,
    '<font="Barlow Condensed"><cspace=0.12em>OXYGEN</cspace> <size=130%><color=#34D399>21.0</color></size> %</font>',
    24, "#E4F1F7")

-- Symbols every loaded face gets, if the font file has them: true minus, en dash, degree, approx
label("symbols", 228, 26,
    '<font="Barlow">\226\136\146 12.5 \194\176C \226\128\147 \226\137\136 0.4 kPa</font>',
    18, "#E4F1F7")

ui:commit()
