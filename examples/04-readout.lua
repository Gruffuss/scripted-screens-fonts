-- 04-readout.lua -- a live readout set in Barlow. Paste into a Lua chip in a ScriptedScreens
-- console.
-- Shows: a font in labels that change every tick. The face is part of the text, so each
-- set_props writes the tag again with the new value. Condensed for titles, regular for values,
-- light for units: three weights of one family keep a dense screen readable.
-- The values here are simulated; replace `sample()` with your device reads.

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

ui:element({
    id = "title",
    type = "label",
    rect = { unit = "px", x = 16, y = 12, w = W - 32, h = 40 },
    props = { text = '<font="Barlow Condensed SemiBold"><cspace=0.08em>ATMOSPHERE</cspace></font>' },
    style = { font_size = 30, color = "#38BDF8", align = "left" },
})

local ROWS = {
    { key = "press", name = "Pressure",    unit = "kPa", fmt = "%.1f" },
    { key = "temp",  name = "Temperature", unit = "\194\176C", fmt = "%.1f" },
    { key = "o2",    name = "Oxygen",      unit = "%",   fmt = "%.1f" },
    { key = "co2",   name = "CO2",         unit = "%",   fmt = "%.2f" },
}

local cells = {}
local top, row = 70, (H - 90) / #ROWS
for i, r in ipairs(ROWS) do
    local y = math.floor(top + (i - 1) * row)
    ui:element({
        id = r.key .. "_name",
        type = "label",
        rect = { unit = "px", x = 16, y = y, w = W / 2, h = math.floor(row) },
        props = { text = '<font="Barlow Condensed">' .. r.name .. '</font>' },
        style = { font_size = 22, color = "#7A93A6", align = "left" },
    })
    cells[r.key] = ui:element({
        id = r.key .. "_value",
        type = "label",
        rect = { unit = "px", x = W / 2, y = y, w = W / 2 - 16, h = math.floor(row) },
        props = { text = "" },
        style = { font_size = 34, color = "#E4F1F7", align = "right" },
    })
end

ui:commit()

local t = 0
local function sample()
    return {
        press = 101.3 + 2.5 * math.sin(t * 0.3),
        temp = 21.0 + 1.5 * math.sin(t * 0.17),
        o2 = 20.9 + 0.4 * math.sin(t * 0.23),
        co2 = 0.04 + 0.02 * (0.5 + 0.5 * math.sin(t * 0.41)),
    }
end

function tick(dt)
    t = t + dt
    local v = sample()
    for _, r in ipairs(ROWS) do
        cells[r.key]:set_props({ text = '<font="Barlow">' .. string.format(r.fmt, v[r.key])
            .. '</font><font="Barlow Light"><size=60%> ' .. r.unit .. '</size></font>' })
    end
    ui:commit()
end
