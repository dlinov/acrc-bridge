# Assetto Corsa RaceChrono Bridge
RaceChrono has support for Assetto Corsa, but Assetto Corsa cannot send GPS-correct coordinates, that's why this bridge is required. With its' help one can select real track in RaceChrono, record sessions and compare them to real racing sessions.

### Idea
Idea is simple: read data from AC, find track GPS coordinates, map it to in-game coordinates and republish AC telemetry in the same format, but with correct GPS

### Requirements
- .NET 10 SDK: `winget install Microsoft.DotNet.SDK.10`

### Useful links
- [RaceChrono forum thread "Assetto Corsa?"](https://racechrono.com/forum/discussion/1892/assetto-corsa)
- [RaceChrono article "Tutorial: DIY devices"](https://racechrono.com/article/2572)
- [AC Socket Document](https://docs.google.com/document/d/1KfkZiIluXZ6mMhLWfDX1qAGbvhGRC3ZUzjVIt5FQpp4/pub)
- [AC forum thread "AC UDP Remote Telemetry"](https://www.assettocorsa.net/forum/index.php?threads/ac-udp-remote-telemetry-update-31-03-2016.222/)
- [Assetto Corsa Remote Telemetry Client](https://github.com/rickwest/ac-remote-telemetry-client) by [@rickwest](https://github.com/rickwest)

### TODO
- Wiki/Instruction how to use with screenshots
- [Issues tab](https://github.com/dlinov/acrc-bridge/issues)

### CI
The workflow is in `.github/workflows/dotnet-desktop.yml` (named **CI** in Actions).

### Creating a release
Releases are tag-based. After your changes are on `master` and CI is green, create and push a tag like:

```bash
git tag v0.1.0
git push origin v0.1.0
```

Pushing the tag triggers `.github/workflows/release.yml`, which builds + tests again and then creates a GitHub Release with:
- `ACRCBridge.App-v0.1.0-linux-x64.tar.gz`
- `ACRCBridge.App-v0.1.0-linux-arm64.tar.gz`
- `ACRCBridge.App-v0.1.0-win-x64.zip`
- `ACRCBridge.App-v0.1.0-win-arm64.zip`
attached.
