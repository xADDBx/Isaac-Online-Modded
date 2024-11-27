local abm3p = RegisterMod("Antibirth Music+++", 1)

local function log(message)
  if true then
    print(message)
  end
end

local RECOMMENDED_SHIFT_IDX = 35
-- RNG object for good rng that doesn't need to be seeded
local randomRNG = RNG()
randomRNG:SetSeed((Random() // 2) + 1, RECOMMENDED_SHIFT_IDX)

local SaveState = {}

-- Mega satan options:
-- 0: play both
-- 1: Flagbearer
-- 2: Spectrum of Sin
local ModConfigSettings = {
  ["passive mode"] = false,
  ["boss intros"] = true,
  ["mega satan"] = 0,
  ["void stage"] = true,
  ["void bosses"] = true,
  ["alt arcade"] = true,
  ["alt shop"] = true,
  ["alt music mode"] = 0,
  ["ridiculon alt alt floors"] = false,
}
-- Incrementing this will overwrite saved settings
local SettingsVersion = 0

local BossIdToName = {
  [6] = "Mom",
  [8] = "Mom's Heart",
  [25] = "Mom's Heart",
  [39] = "Isaac",
  --    [24] = "Satan",
  [40] = "Blue Baby",
  [54] = "Lamb",
  [70] = "Delirium",
  [88] = "Mother",
  [62] = "Ultra Greed",
  [63] = "Blue Baby", -- Hush
}

-- Tracks for void that play randomly
-- The Void 0 is the original track with intro
-- Other ones are only loops starting from a different point
local VoidMusic = {
  [1] = Isaac.GetMusicIdByName("The Void 0"),
  [2] = Isaac.GetMusicIdByName("The Void 1"),
  [3] = Isaac.GetMusicIdByName("The Void 2"),
  [4] = Isaac.GetMusicIdByName("The Void 3"),
  [5] = Isaac.GetMusicIdByName("The Void 4"),
  [6] = Isaac.GetMusicIdByName("The Void 5"),
  [7] = Isaac.GetMusicIdByName("The Void 6"),
  [8] = Isaac.GetMusicIdByName("The Void 7"),
}

local function has_value(tab, val)
  for index, value in ipairs(tab) do
    if value == val then
      return true
    end
  end

  return false
end

local currentVoidMusicId = 0

-- Randomized room music
local RandomAltMusic = nil
local function updateRandomAltMusic()
  local rng = RNG()
  if ModConfigSettings["alt music mode"] == 1 then
    local game = Game()
    local seeds = game:GetSeeds()
    rng:SetSeed(seeds:GetStageSeed(game:GetLevel():GetStage()), RECOMMENDED_SHIFT_IDX)
  else
    local seed = Random()
    if seed < 1 or seed > 4294967295 then
      seed = 256
    end
    rng:SetSeed(randomRNG:RandomInt(4294967295) + 1, RECOMMENDED_SHIFT_IDX)
  end
  RandomAltMusic = {
    ["Arcade"] = rng:RandomInt(10),
    ["Shop"] = rng:RandomInt(20),
    -- ["Arcade"] = 1,
    -- ["Shop"] = 1,
  }
end

updateRandomAltMusic()
abm3p:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, updateRandomAltMusic)

local function init()
  log("Initializing Antibirth Music+++")

  if ModConfigSettings["passive mode"] then
    log("ABM+++ in passive mode")
  else
    if MMC then
      log("ABM+++ in MMC mode")

      -- ===============
      -- Story boss intros
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        if ModConfigSettings["boss intros"] then
          local trackName = BossIdToName[Game():GetRoom():GetBossID()]
          if trackName then
            return Isaac.GetMusicIdByName("True " .. trackName)
          end
        else
          return nil
        end
      end, Music.MUSIC_JINGLE_BOSS)

      -- Play the boss theme manually after the versus screen
      MMC.AddMusicCallback(abm3p, function()
        if ModConfigSettings["boss intros"] then
          local currentMusic = MusicManager():GetCurrentMusicID()
          if (currentMusic == Music.MUSIC_JINGLE_GAME_START or currentMusic == Music.MUSIC_JINGLE_GAME_START_ALT) then
            -- Resuming from menu, play normal version of track
            local trackName = BossIdToName[Game():GetRoom():GetBossID()]
            return Isaac.GetMusicIdByName(trackName)
          else
            -- Coming from boss vs screen, music is already playing
            return 0
          end
        else
          local trackName = BossIdToName[Game():GetRoom():GetBossID()]
          return Isaac.GetMusicIdByName(trackName)
        end
      end, Music.MUSIC_MOM_BOSS, Music.MUSIC_MOMS_HEART_BOSS, Music.MUSIC_ISAAC_BOSS, Music.MUSIC_DARKROOM_BOSS,
        Music.MUSIC_BLUEBABY_BOSS, Music.MUSIC_ULTRAGREED_BOSS, Music.MUSIC_MOTHER_BOSS, Music.MUSIC_VOID_BOSS)

      -- ===============
      -- Mega satan music
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        local stage = Game():GetLevel():GetStage()
        if stage == LevelStage.STAGE6 then
          -- Megasatan music is about to play
          if (randomRNG:RandomInt(2) == 0 or ModConfigSettings["mega satan"] == 1)
              and ModConfigSettings["mega satan"] ~= 2 then
            return Isaac.GetMusicIdByName("Mega Satan")
          else
            return Isaac.GetMusicIdByName("Satan")
          end
        end
      end, Music.MUSIC_SATAN_BOSS)

      -- ===============
      -- Void music variations
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        if ModConfigSettings["void stage"] then
          if Game():GetRoom():IsFirstVisit() and not has_value(VoidMusic, MusicManager():GetCurrentMusicID()) then
            currentVoidMusicId = VoidMusic[1]
            return VoidMusic[1]
          else
            if MusicManager():GetCurrentMusicID() == currentVoidMusicId then
              return 0
            else
              currentVoidMusicId = VoidMusic[randomRNG:RandomInt(7) + 2]
              return currentVoidMusicId
            end
          end
        else
          return VoidMusic[1]
        end
      end, Music.MUSIC_VOID)

      -- Void boss music
      MMC.AddMusicCallback(abm3p, function()
        if Game():GetLevel():GetStage() == LevelStage.STAGE7 and ModConfigSettings["void bosses"] then
          return Isaac.GetMusicIdByName("The Void Boss " .. randomRNG:RandomInt(5) + 1)
        else
          return nil
        end
      end, Music.MUSIC_BOSS, Music.MUSIC_BOSS2)

      -- ===============
      -- Greed mode mom music on last wave
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        if Game():GetRoom():GetType() == RoomType.ROOM_DEFAULT then
          return Isaac.GetMusicIdByName("Mom")
        end
      end, Music.MUSIC_SATAN_BOSS)

      MMC.AddMusicCallback(abm3p, function()
        local MusicM = MusicManager()
        local currentMusic = MusicM:GetCurrentMusicID()
        if currentMusic == Isaac.GetMusicIdByName("Mom") and Game():GetRoom():GetType() == RoomType.ROOM_DEFAULT then
          return Music.MUSIC_JINGLE_BOSS_OVER, Music.MUSIC_BOSS_OVER
        end
      end, Music.MUSIC_BOSS_OVER)

      -- ===============
      -- Ambush theme with intro for challenge rooms
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        if Game():GetRoom():GetType() == RoomType.ROOM_CHALLENGE then
          return Isaac.GetMusicIdByName("Ambush (Full)")
        end
      end, Music.MUSIC_CHALLENGE_FIGHT)

      -- ===============
      -- Alt tracks that play randomly
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        if ModConfigSettings["alt arcade"] and RandomAltMusic["Arcade"] == 0 then
          return Isaac.GetMusicIdByName("Arcade Alt")
        else
          return nil
        end
      end, Music.MUSIC_ARCADE_ROOM)
      MMC.AddMusicCallback(abm3p, function()
        if ModConfigSettings["alt shop"] and RandomAltMusic["Shop"] == 0 then
          print("Play alternate shop")
          return Isaac.GetMusicIdByName("Shop Alt")
        else
          return nil
        end
      end, Music.MUSIC_SHOP_ROOM)

      -- ===============
      -- Special boss outros for Mother and Hush
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        if Game():GetRoom():GetBossID() == 88 then
          return Isaac.GetMusicIdByName("Boss Mother Death (jingle) MMC")
        end
      end, Music.MUSIC_JINGLE_BOSS_OVER3)

      MMC.AddMusicCallback(abm3p, function()
        if Game():GetRoom():GetBossID() == 63 then
          return Isaac.GetMusicIdByName("Boss Hush Death (jingle)")
        end
      end, Music.MUSIC_JINGLE_BOSS_OVER2, Music.MUSIC_JINGLE_BOSS_OVER)

      -- ===============
      -- Play Ridiculon versions of alt alt floor tracks
      -- ===============
      MMC.AddMusicCallback(abm3p, function(self, musicId)
        if ModConfigSettings["ridiculon alt alt floors"] then
          local RidiculonTracks = {
            [Music.MUSIC_DROSS] = Isaac.GetMusicIdByName("Night Soil"),
            [Music.MUSIC_ASHPIT] = Isaac.GetMusicIdByName("Absentia"),
            [Music.MUSIC_GEHENNA] = Isaac.GetMusicIdByName("Morning Star"),
            [Music.MUSIC_DROSS_REVERSE] = Isaac.GetMusicIdByName("Night Soil (reversed)")
          }

          return RidiculonTracks[musicId]
        end
      end)


      -- ===============
      -- Play fixed version of songs MMC breaks
      -- ===============
      MMC.AddMusicCallback(abm3p, function(self, musicId)
        local brokenTracks = {
          [Music.MUSIC_BOSS] = "Boss MMC",
          [Music.MUSIC_UTERO] = "Utero MMC",
          [Music.MUSIC_SATAN_BOSS] = "Boss (Sheol - Satan) MMC",
          [Music.MUSIC_BOSS3] = "Boss (alternate alternate) MMC",
          [Music.MUSIC_JINGLE_MOTHER_OVER] = "Boss Mother Death (jingle) MMC",
          [Music.MUSIC_PLANETARIUM] = "Planetarium MMC",
          [Music.MUSIC_DOWNPOUR] = "Downpour MMC",
          [Music.MUSIC_MINES] = "Mines MMC",
          [Music.MUSIC_MAUSOLEUM] = "Mausoleum MMC",
          [Music.MUSIC_CORPSE] = "Corpse MMC",
          [Music.MUSIC_DROSS] = "Dross MMC",
          [Music.MUSIC_ASHPIT] = "Ashpit MMC",
          [Music.MUSIC_GEHENNA] = "Gehenna MMC",
        }

        if brokenTracks[musicId] then
          return Isaac.GetMusicIdByName(brokenTracks[musicId])
        else
          return nil
        end
      end)

      -- ===============
      -- Play unused Treasure Jingles for TMTRAINER
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        local hasTmtrainer = false
        local numPlayers = Game():GetNumPlayers()
        for i = 0, numPlayers do
          local player = Isaac.GetPlayer(i)
          if player:HasCollectible(CollectibleType.COLLECTIBLE_TMTRAINER) then
            hasTmtrainer = true
            break;
          end
        end

        if hasTmtrainer then
          return 0, nil, Isaac.GetSoundIdByName("Treasure Jingle (antibirth, unused)")
        else
          return nil
        end
      end, Music.MUSIC_JINGLE_TREASUREROOM_ENTRY_0, Music.MUSIC_JINGLE_TREASUREROOM_ENTRY_1,
        Music.MUSIC_JINGLE_TREASUREROOM_ENTRY_2, Music.MUSIC_JINGLE_TREASUREROOM_ENTRY_3)

      -- ===============
      -- Play fixed version of jingles MMC breaks
      -- ===============
      MMC.AddMusicCallback(abm3p, function()
        return 0, nil, Isaac.GetSoundIdByName("Secret Room Jingle (antibirth)")
      end, Music.MUSIC_JINGLE_SECRETROOM_FIND)

      MMC.AddMusicCallback(abm3p, function()
        return 0, nil, Isaac.GetSoundIdByName("Treasure Jingle (antibirth)")
      end, Music.MUSIC_JINGLE_TREASUREROOM_ENTRY_0, Music.MUSIC_JINGLE_TREASUREROOM_ENTRY_1,
        Music.MUSIC_JINGLE_TREASUREROOM_ENTRY_2, Music.MUSIC_JINGLE_TREASUREROOM_ENTRY_3)
    else
      log("ABM+++ in non-MMC mode")

      -- ===============
      -- Story boss intros
      -- ===============
      abm3p:AddCallback(ModCallbacks.MC_POST_RENDER, function()
        local currentMusic = MusicManager():GetCurrentMusicID()
        local room = Game():GetRoom()

        -- Play boss intro
        local function crossfadeFunc(trackName, currentMusic)
          local trackId = Isaac.GetMusicIdByName("True " .. trackName)
          if trackId and currentMusic ~= trackId then
            MusicManager():Crossfade(trackId)
          end
        end

        -- Queue boss track (when loading from main menu)
        local function queueFunc(trackName, currentMusic)
          local trackId = Isaac.GetMusicIdByName(trackName)
          if trackId and currentMusic ~= trackId then
            MusicManager():Queue(trackId, 0)
          end
        end

        local trackMode = nil

        -- TrackMode == 1 -> main mode, play full version
        -- TrackMode == 2 -> queue the music after loading the game from main menu
        if currentMusic == Music.MUSIC_JINGLE_BOSS then
          trackMode = 1
          abm3p.PlayFunc = crossfadeFunc
        elseif room:GetType() == RoomType.ROOM_BOSS
            and (currentMusic == Music.MUSIC_JINGLE_GAME_START or currentMusic == Music.MUSIC_JINGLE_GAME_START_ALT) then
          trackMode = 2
          abm3p.PlayFunc = queueFunc
        end

        if trackMode then
          local trackName = BossIdToName[Game():GetRoom():GetBossID()]
          -- if Story Boss Intros are disabled, play the music after the intro screen is finished
          if room:GetAliveBossesCount() > 0 and trackName and
              (ModConfigSettings["boss intros"] or Game():GetHUD():IsVisible()) then
            abm3p.PlayFunc(trackName, currentMusic)
          end
        end
      end)

      -- ===============
      -- Mega satan music
      -- ===============
      abm3p:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
        local MusicM = MusicManager()
        local currentMusic = MusicM:GetCurrentMusicID()
        local stage = Game():GetLevel():GetStage()

        --unique Mega Satan music
        if stage == LevelStage.STAGE6 and currentMusic == Music.MUSIC_SATAN_BOSS then
          if (randomRNG:RandomInt(2) == 0 or ModConfigSettings["mega satan"] == 1)
              and ModConfigSettings["mega satan"] ~= 2 then
            MusicM:Play(Isaac.GetMusicIdByName("Mega Satan"), 0)
            MusicM:UpdateVolume()
          else
            MusicM:Play(Isaac.GetMusicIdByName("Satan"), 0)
            MusicM:UpdateVolume()
          end
        end
      end)

      -- ===============
      -- Void music variations
      -- ===============
      abm3p:AddCallback(ModCallbacks.MC_POST_RENDER, function()
        local MusicM = MusicManager()
        local currentMusic = MusicM:GetCurrentMusicID()

        -- Stage music
        if Game():GetLevel():GetStage() == LevelStage.STAGE7
            and Game():GetRoom():GetType() == RoomType.ROOM_DEFAULT
            and not has_value(VoidMusic, currentMusic)
            and currentMusic ~= Music.MUSIC_JINGLE_GAME_OVER
            and currentMusic ~= Music.MUSIC_GAME_OVER then
          if currentMusic == Music.MUSIC_JINGLE_GAME_START
              or currentMusic == Music.MUSIC_JINGLE_GAME_START_ALT then
            MusicM:Queue(VoidMusic[1])
          elseif Game():GetRoom():IsFirstVisit()
              or not ModConfigSettings["void stage"] then
            MusicM:Crossfade(VoidMusic[1])
          else
            MusicM:Crossfade(VoidMusic[randomRNG:RandomInt(7) + 2])
          end
        end

        -- Ordinary bosses
        if Game():GetLevel():GetStage() == LevelStage.STAGE7
            and (currentMusic == Music.MUSIC_BOSS or currentMusic == Music.MUSIC_BOSS2)
            and ModConfigSettings["void bosses"] then
          local trackId = Isaac.GetMusicIdByName("The Void Boss " .. randomRNG:RandomInt(5) + 1)
          --trackId = Isaac.GetMusicIdByName("The Void Boss " .. 5)
          MusicM:Crossfade(trackId)
        end
      end)

      -- ===============
      -- Greed mode mom music on last wave
      -- ===============
      abm3p:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
        local MusicM = MusicManager()
        local currentMusic = MusicM:GetCurrentMusicID()

        if Game():GetRoom():GetType() == RoomType.ROOM_DEFAULT
            and currentMusic == Music.MUSIC_SATAN_BOSS then
          MusicM:Play(Isaac.GetMusicIdByName("Mom"), 0)
          MusicM:UpdateVolume()
        end
        if Game():GetRoom():IsClear() and currentMusic == Isaac.GetMusicIdByName("Mom")
            and currentMusic ~= Music.MUSIC_JINGLE_BOSS_OVER then
          MusicM:Play(Music.MUSIC_JINGLE_BOSS_OVER, 0)
          MusicM:UpdateVolume()
          MusicM:Queue(Music.MUSIC_BOSS_OVER)
        end
      end)

      -- ===============
      -- Ambush theme with intro for challenge rooms
      -- ===============
      abm3p:AddCallback(ModCallbacks.MC_POST_RENDER, function()
        local MusicM = MusicManager()
        local currentMusic = MusicM:GetCurrentMusicID()

        if Game():GetRoom():GetType() == RoomType.ROOM_CHALLENGE
            and currentMusic == Music.MUSIC_CHALLENGE_FIGHT then
          MusicM:Play(Isaac.GetMusicIdByName("Ambush (Full)"), 0)
          MusicM:UpdateVolume()
        end
      end)

      -- ===============
      -- Alt tracks that play randomly
      -- ===============
      abm3p:AddCallback(ModCallbacks.MC_POST_RENDER, function()
        local MusicM = MusicManager()
        local currentMusic = MusicM:GetCurrentMusicID()

        if currentMusic == Music.MUSIC_ARCADE_ROOM
            and RandomAltMusic["Arcade"] == 1
            and ModConfigSettings["alt arcade"] then
          MusicM:Play(Isaac.GetMusicIdByName("Arcade Alt"), 0)
          MusicM:UpdateVolume()
        end
        if currentMusic == Music.MUSIC_SHOP_ROOM
            and RandomAltMusic["Shop"] == 1
            and ModConfigSettings["alt shop"] then
          MusicM:Play(Isaac.GetMusicIdByName("Shop Alt"), 0)
          MusicM:UpdateVolume()
        end
      end)

      -- ===============
      -- Special boss outros for Mother and Hush
      -- ===============
      abm3p:AddCallback(ModCallbacks.MC_POST_RENDER, function()
        local MusicM = MusicManager()
        local currentMusic = MusicM:GetCurrentMusicID()

        if currentMusic == Isaac.GetMusicIdByName("Boss Death Alternate Alternate (jingle)") and
            Game():GetRoom():GetBossID() == 88 then
          MusicM:Play(Isaac.GetMusicIdByName("Boss Mother Death (jingle)"), 0)
          MusicM:Queue(Isaac.GetMusicIdByName("Boss Room (empty)"), 0)
          MusicM:UpdateVolume()
        end

        if (
            currentMusic == Isaac.GetMusicIdByName("Boss Death Alternate (jingle)") or
            currentMusic == Isaac.GetMusicIdByName("Boss Death (jingle)")) and
            Game():GetRoom():GetBossID() == 63 then
          MusicM:Play(Isaac.GetMusicIdByName("Boss Hush Death (jingle)"), 0)
          MusicM:Queue(Isaac.GetMusicIdByName("Boss Room (empty)"), 0)
          MusicM:UpdateVolume()
        end
      end)
    end
  end
end

-- ===============
-- Settings and Mod Config Menu
-- ===============
if ModConfigMenu then
  local function AddBoolSetting(category, optionName, shortDescription, description)
    local BoolValues = {
      [true] = "On",
      [false] = "Off"
    }

    ModConfigMenu.AddSetting(category, {
      Type = ModConfigMenu.OptionType.BOOLEAN,
      CurrentSetting = function() return ModConfigSettings[optionName] end,
      Display = function() return shortDescription .. ": " .. BoolValues[ModConfigSettings[optionName]] end,
      OnChange = function(val) ModConfigSettings[optionName] = val end,
      Info = { description }
    })
  end

  log("Mod Config Menu detected, loading ABM+++ settings")

  local category = "Antibirth Music+++"

  ModConfigMenu.UpdateCategory(category, {
    Info = "Mudeth music mod with tweaks",
  })

  AddBoolSetting(category, "passive mode", "Passive Mode", "For usage with Soundtrack Menu, requires restart")

  AddBoolSetting(category, "boss intros", "Story Boss Intros", "Boss music starts playing during the intro screen")

  -- Mega Satan
  local MegaSatanOptionValues = {
    [0] = "Play both versions",
    [1] = "Flagbearer",
    [2] = "Spectrum of Sin",
  }

  local AltMusicOptionValues = {
    [0] = "Per time entering the room",
    [1] = "Per floor"
  }

  ModConfigMenu.AddSetting(category, {
    Type = ModConfigMenu.OptionType.NUMBER,
    CurrentSetting = function() return ModConfigSettings["mega satan"] end,
    Minimum = 0,
    Maximum = 2,
    Display = function() return "Mega Satan music: " .. MegaSatanOptionValues[ModConfigSettings["mega satan"]] end,
    OnChange = function(val) ModConfigSettings["mega satan"] = val end,
    Info = { "Mega Satan music option" }
  })

  AddBoolSetting(category, "void stage", "Random stage music in the Void",
    "Music plays from a random starting position in the Void")
  AddBoolSetting(category, "void bosses", "Random boss music in the Void",
    "Ordinary bosses in the Void have random music from different Isaac composers")
  AddBoolSetting(category, "alt arcade", "Alt arcade music",
    "DannyB's arcademusic theme can be played with 10% probability")
  AddBoolSetting(category, "alt shop", "Alt shop music",
    "DannyB's shop theme can be played with 5% probability")

  ModConfigMenu.AddSetting(category, {
    Type = ModConfigMenu.OptionType.NUMBER,
    CurrentSetting = function() return ModConfigSettings["alt music mode"] end,
    Minimum = 0,
    Maximum = 1,
    Display = function() return "Alt Music Mode: " .. AltMusicOptionValues[ModConfigSettings["alt music mode"]] end,
    OnChange = function(val) ModConfigSettings["alt music mode"] = val end,
    Info = { "Alt Music Mode" }
  })

  AddBoolSetting(category, "ridiculon alt alt floors", "Ridiculon alt alt floor music",
    "[[Music Callback Mod ONLY]] Use Ridiculon's Repentance tracks for Dross, Ashpit and Gehenna.")
end

local json = require("json")

function abm3p:SaveGame()
  SaveState.Settings = {}
  SaveState.Settings["version"] = SettingsVersion

  for i, v in pairs(ModConfigSettings) do
    SaveState.Settings[tostring(i)] = v
  end
  abm3p:SaveData(json.encode(SaveState))
end

abm3p:AddCallback(ModCallbacks.MC_PRE_GAME_EXIT, abm3p.SaveGame)


function abm3p:OnGameStart(isSave)
  if abm3p:HasData() then
    SaveState = json.decode(abm3p:LoadData())

    for i, v in pairs(SaveState.Settings) do
      ModConfigSettings[tostring(i)] = v
    end
  end
  init()
end

abm3p:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, abm3p.OnGameStart)

--Isaac.RenderText(,100,100,255,0,0,255)
--Isaac.FindByType(EntityType.ENTITY_EFFECT, EffectVariant.ANGEL, -1)
