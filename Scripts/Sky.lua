local Sky = {}
local day_packet = { 76, 240, 23, 1, 0, 0, 0, 12, 67, 0, 0, 112, 65, 26, 164, 86, 62, 63, 54, 218, 59, 109, 238, 60, 63, 0, 32, 205, 59, 9, 91, 47, 192, 221, 7, 0, 0, 2, 21, 154, 225, 118, 65 }

local Encoding = luanet.import_type "System.Text.Encoding"
local DateTime = luanet.import_type "System.DateTime"

function Sky:filter(wrapper, night, day)
    if wrapper.IsIncoming then
        if wrapper.PacketData:GetValue(0) == 0x89 then
            local s = Encoding.ASCII:GetString(wrapper.PacketData, 8, wrapper.PacketData:GetValue(7))
            if s == "CL_UpdateSkyState" then
                print(string.format("[%s] RPC: CL_UpdateSkyState", DateTime.UtcNow:ToString()))
                if night then
                    for i = 25, wrapper.PacketSize do
                        wrapper.PacketData[i] = 0
                    end
                elseif day then
                    for i = 25, wrapper.PacketSize do
                        wrapper.PacketData[i] = day_packet[i - 24]
                    end
                end
            end
        end
    end
    return true
end

return Sky
