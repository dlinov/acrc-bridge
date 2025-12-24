# Assetto Corsa RaceChrono Bridge
RaceChrono has support for Assetto Corsa, but Assetto Corsa cannot send GPS-correct coordinates, that's why this bridge is required. With its' help one can select real track in RaceChrono, record sessions and compare them to real racing sessions.

### Idea
Idea is simple: read data from AC, find track GPS coordinates, map it to in-game coordinates and republish AC telemetry in the same format, but with correct GPS

### Requirements
- .NET 9 SDK: `winget install Microsoft.DotNet.SDK.9`

### Useful links
- [RaceChrono forum thread "Assetto Corsa?"](https://racechrono.com/forum/discussion/1892/assetto-corsa)
- [RaceChrono article "Tutorial: DIY devices"](https://racechrono.com/article/2572)
- [AC Socket Document](https://docs.google.com/document/d/1KfkZiIluXZ6mMhLWfDX1qAGbvhGRC3ZUzjVIt5FQpp4/pub)
- [AC forum thread "AC UDP Remote Telemetry"](https://www.assettocorsa.net/forum/index.php?threads/ac-udp-remote-telemetry-update-31-03-2016.222/)
- [Assetto Corsa Remote Telemetry Client](https://github.com/rickwest/ac-remote-telemetry-client) by [@rickwest](https://github.com/rickwest)

### TODO
- Github build and releases
- Wiki/Instruction how to use with screenshots
