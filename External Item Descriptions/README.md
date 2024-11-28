### External Item Descriptions
Just changing two settings in the main.lua of the mod to true should make it work. 
This mod ***works even if only one person has it enabled***.

You can either replaced your EID main.lua with the one here or manually change the following values in the main.lua of the mod to true:
```
EID.isMultiplayer = false -- Used to color P1's highlight/outline indicators (single player just uses white)
EID.isOnlineMultiplayer = false -- Set to true to disable code functions that might cause desyncs
```
=>
```
EID.isMultiplayer = true -- Used to color P1's highlight/outline indicators (single player just uses white)
EID.isOnlineMultiplayer = true -- Set to true to disable code functions that might cause desyncs
```
