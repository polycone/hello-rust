local PacketLogger = {}

function PacketLogger:log(packet)
    if packet.IsIncoming then
        print(string.format("Incoming [%d]", packet.PacketSize))
    else
        print(string.format("Outgoing [%d]", packet.PacketSize))
    end
    return false
end

return PacketLogger
