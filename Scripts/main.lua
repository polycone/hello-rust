PacketLogger = require "scripts/PacketLogger"
Sky = require "scripts/Sky"

DateTime = luanet.import_type("System.DateTime")

function ypos(y)
    return (checkbox.height + 3) * y
end

function xpos(x)
    return 100 * x
end

function initialize()
    checkbox.add("log", "Log packets", xpos(0), ypos(0), true)
    checkbox.add("sky", "Filter Sky", xpos(0), ypos(1), true)
    checkbox.add("skyn", "Night", xpos(0), ypos(2), true)
    checkbox.add("skyd", "Day", xpos(0), ypos(3), true)
end

function main(packet)
    if checkbox.get("log") then
        PacketLogger:log(packet)
    end
    if checkbox.get("sky") then
        Sky:filter(packet, checkbox.get("skyn"), checkbox.get("skyd"))
    end
end


function finalize()
    
end
