GRAND THEFT BARFENDERS - mod for Barfender
==========================================

Trevor Philips, the arms dealer, stands behind your bar and sells:
  - Halloween pistol (from the shooting gallery), 25 rounds ... 150
  - Club that looks like the mop ............................. 75
Money comes from the bar's cash (no experience points are lost).
Every pistol hit and every club hit counts exactly like a punch with your fist -
against customers and other players. Hitting your own staff with the club does
no damage: it drives them on instead (20 seconds, +50 % walking speed).

REQUIREMENTS
  - MelonLoader 0.7.3  https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3

INSTALL
  1. Close Barfender and open the game folder
     (Steam: right-click Barfender > Manage > Browse local files).
  2. Install MelonLoader 0.7.3 (skip if you already use another MelonLoader mod):
     run the official MelonLoader installer, pick version 0.7.3 and Barfender.exe -
     or extract MelonLoader.x64.zip from the v0.7.3 release into the game folder.
     You then have version.dll and a MelonLoader folder next to Barfender.exe.
  3. Folder link (skip if "Barfender_Data" already exists): MelonLoader looks for
     "Barfender_Data", the game folder is called "Data". Open a command prompt in
     the game folder and run:
         mklink /J Barfender_Data Data
     This is only a link, not a copy.
  4. Copy the "Mods" and "Plugins" folders from this archive into the game folder
     (merge them with existing folders):
         Mods\GrandTheftBarfenders.dll
         Plugins\Il2CppInteropUnity6000Fix.dll   <- required, see below
  5. Start Barfender. The first start with MelonLoader takes 1-3 minutes and an
     extra black console window opens - that's normal. It should show
     "Grand Theft Barfenders" and "Mods loaded".

  Il2CppInteropUnity6000Fix.dll: without it MelonLoader crashes on Barfender's
  Unity version a few seconds after start. If another mod already installed it,
  keep the newer one.

IN GAME
  E ............ look at Trevor behind the bar -> shop
  1 ............ draw / holster the pistol
  2 ............ draw / holster the club
  Left mouse ... shoot / hit
  F6 ........... adjust how the weapons sit in the hand (arrows / Page up/down move,
                 hold Shift to rotate, Backspace resets)
  While a weapon is drawn you can't pick up, use, drop or throw things.
  Keys can be changed in UserData\MelonPreferences.cfg, section [GrandTheftBarfenders].

CO-OP
  The host needs the mod (Trevor is a real game character, so guests see him too).
  Guests need the mod to buy and use weapons. Players with the mod see each other's
  weapons (shared through the Steam lobby); players without it don't.

UPDATE
  Close the game and overwrite Mods\GrandTheftBarfenders.dll. Ammo, club and
  settings are kept.

UNINSTALL
  Delete Mods\GrandTheftBarfenders.dll. If no other mods are left, you can also
  delete Plugins\Il2CppInteropUnity6000Fix.dll, version.dll, the MelonLoader folder
  and the link (command prompt in the game folder: rmdir Barfender_Data - this only
  removes the link, never the game's Data folder).
  Trevor stays in your savegames as an idle character.
