# NoMulticrew

A [BepInEx](https://github.com/BepInEx/BepInEx) plugin for **Nuclear Option** that puts a second player in
the back seat of an aircraft another player is flying.

> [!IMPORTANT]
> Multicrew needs the mod on the **server** and on **both** crew members' clients. It is never needed to
> connect: on a server without it, a modded client plays vanilla and is indistinguishable from one.

## Status

Foundation only. The plugin currently detects multicrew-capable servers, badges them in the browser and
identifies itself to them. Crews cannot form yet.

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx) into your Nuclear Option folder.
2. Put `NoMulticrew.dll` into `BepInEx/plugins`.

## License

[Apache-2.0](./LICENSE)
