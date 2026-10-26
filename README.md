# Grand Theft Barfenders

A [MelonLoader](https://github.com/LavaGang/MelonLoader) mod for **Barfender**.

Trevor Philips sets up shop behind your bar, selling the Halloween shooting-gallery pistol
(25 rounds) and a mop-style club. Every hit counts as a punch on customers and players, while
clubbing your staff gives them +50 % walking speed for 20 seconds. Co-op ready: players with the
mod see each other's weapons.

Player instructions (install, controls, co-op): see [`package/README-Nexus.txt`](package/README-Nexus.txt).

## What's in this repository

| Folder | Content |
|---|---|
| `GrandTheftBarfenders/` | The mod (`Mods/GrandTheftBarfenders.dll`) |
| `Il2CppInteropUnity6000Fix/` | Required MelonLoader plugin (`Plugins/Il2CppInteropUnity6000Fix.dll`), see below |
| `package/` | Files for the release archives (README, optional installer scripts) |
| `tools/package.ps1` | Builds both projects and the release archives |

Every DLL in the release archives is built from the source in this repository.

### Why the fix plugin is needed

Without it, MelonLoader crashes on Barfender (Unity 6000.3) a few seconds after start, with or
without mods (LavaGang/MelonLoader#1159, BepInEx/Il2CppInterop#283). Il2CppInterop 1.5.1 locates
`Class::GetDefaultFieldValue` by following a chain of jumps from `il2cpp_field_static_get_value`;
on Unity 6000.3 that chain ends inside the constant decoder at an address in the middle of an
instruction, and the hook placed there corrupts machine code. The plugin patches Il2CppInterop's
lookup with Harmony: if the found address isn't instruction-aligned, it uses the first `call`
target in `Field::StaticGetValue` instead (the real `Class::GetDefaultFieldValue`). It hard-codes
no game addresses and does nothing when Il2CppInterop finds the right address on its own.

## Building

Requirements:

- Windows, [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or newer
- Barfender with **MelonLoader 0.7.3** installed, started once so MelonLoader generates
  `MelonLoader\Il2CppAssemblies` (the projects reference the game's generated interop assemblies;
  they are not included here)

Build (replace the path with your game folder):

```bash
dotnet build GrandTheftBarfenders -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Barfender"
dotnet build Il2CppInteropUnity6000Fix -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Barfender"
```

By default the builds copy the results into the game's `Mods` / `Plugins` folders; add
`-p:CopyToMods=false -p:CopyToPlugins=false` to only build. Outputs:
`GrandTheftBarfenders/bin/Release/net6.0/GrandTheftBarfenders.dll` and
`Il2CppInteropUnity6000Fix/bin/Release/net6.0/Il2CppInteropUnity6000Fix.dll`.

Release archives (adjust `GameDir` in the `.csproj` files or pass it as above):

```bash
powershell -ExecutionPolicy Bypass -File tools/package.ps1
```

creates `dist/GrandTheftBarfenders-<version>-Nexus.zip` (DLLs + README only) and
`dist/GrandTheftBarfenders-<version>.zip` (with an optional installer script).
