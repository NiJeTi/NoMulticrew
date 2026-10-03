# NoMulticrew

A [BepInEx](https://github.com/BepInEx/BepInEx) plugin for **Nuclear Option** that puts a second player in
the back seat of an aircraft another player is flying.

> [!IMPORTANT]
> Multicrew needs the mod on the **server** and on **both** crew members' clients. It is never needed to
> connect: on a server without it, a modded client plays vanilla and is indistinguishable from one.

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx) into your Nuclear Option folder.
2. Install [Extra Input Framework](https://github.com/Assassin1076/NuclearOptionInputFramework/releases) into
   `BepInEx/plugins`.
3. Put `NoMulticrew.dll` into `BepInEx/plugins`.

## Usage

1. A pilot parks at an airbase. Another player on the same side opens the deploy menu at that airbase and
   picks the crew seat from the list.
2. The pilot accepts with the accept key, or from the **CRW** page on the map's MFD.
3. The back-seater sees from their own seat, with the full HUD and their own radar picture. Using the back
   seat's weapons and sensors arrives in a later version.
4. To leave, use the leave key or the CRW page's **LEAVE**. This works on the ground only.

The keys are bound under Controls → Gameplay, as:

- `NoMulticrew: Leave crew seat`
- `NoMulticrew: Accept crew request`
- `NoMulticrew: Decline crew request`

## License

[Apache-2.0](./LICENSE)
