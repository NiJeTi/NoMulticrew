# NoMulticrew

A [BepInEx](https://github.com/BepInEx/BepInEx) plugin for **Nuclear Option** that puts other players in the crew seats of an aircraft another player is flying — a WSO, a door gunner, or several at once.

> [!IMPORTANT]
> Multicrew needs the mod on the **server** and on **every** crew member's client. It is never needed to
> connect: on a server without it, a modded client plays vanilla and is indistinguishable from one.

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx) into your Nuclear Option folder.
2. Put `NoMulticrew.dll` into `BepInEx/plugins`.

## Playing

- **Boarding:** with no aircraft, open the map and the **CRW** page. Every free seat at your faction's airbases is listed;
  press **REQUEST** and wait for the pilot to accept. Aircraft on the ground near an airbase, below 50 km/h, take crew.
- **Answering:** as the pilot, open the map; **ACCEPT** or **DECLINE** on the CRW page. **REQUESTS** turns requests off.
- **Leaving:** press **Eject**. Stopped at an airbase you leave at once and are paid your crew earnings. Anywhere else,
  press it twice within 3 seconds: you bail out and your earnings go to the pilot.

## License

[Apache-2.0](./LICENSE)
