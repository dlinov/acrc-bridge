# Assetto Corsa RaceChrono Bridge
RaceChrono has support for Assetto Corsa, but Assetto Corsa cannot send GPS-correct coordinates, that's why this bridge is required. With its' help one can select real track in RaceChrono, record sessions and compare them to real racing sessions.

### Idea
Idea is simple: read data from AC, find track GPS coordinates, map it to in-game coordinates and republish AC telemetry in the same format, but with correct GPS

### Requirements
- .NET 10 SDK: `winget install Microsoft.DotNet.SDK.10`

### RaceChrono setup
Add a **RaceChrono DIY** device using a TCP/IP connection:

- Host: the address printed as `awaiting RaceChrono connection at ...` by the bridge
- Port: `Bridge:Port` from `appsettings.json` (`19996` by default)
- NMEA 0183: enabled (GPS position, speed and bearing)
- RC2/RC3: enabled (RPM, acceleration, gear and analog channels)

RPM uses RC3's fixed `rpm/d1` (`Digital 1/RPM`) field. If the bridge dashboard shows
a non-zero RPM but RaceChrono does not, verify that RC2/RC3 is enabled and select the
RPM channel in the RaceChrono live screen or analysis view. If the dashboard also shows
zero RPM, the value is not arriving from Assetto Corsa.

### Assetto Corsa telemetry channels
The bridge reads Assetto Corsa's UDP remote telemetry (`RTCarInfo`) and, when it runs on
the **same machine** as Assetto Corsa, also reads an opt-in **shared-memory** block
(enabled with `Games:AssettoCorsa:SameMachineWithAC`, `true` in the shipped
`appsettings.json`). The shared-memory reader unlocks channels that are not available over
UDP. All channels below can be mapped to RaceChrono `$RC3` slots (see next section).

**From UDP (always available):**

| Channel | Meaning | Unit |
| --- | --- | --- |
| `SpeedKmh` | vehicle speed | km/h |
| `EngineRpm` | engine speed | rpm |
| `Gear` | gear index (0=R, 1=N, 2=1st, …) | index |
| `Gas` | throttle | 0..1 |
| `Brake` | brake | 0..1 |
| `Clutch` | clutch (may be inverted via `InvertClutch`) | 0..1 |

`Latitude`/`Longitude`/`Altitude`, `PosNormalized`, `Slope`, and
`LapTime`/`LastLap`/`BestLap`/`LapCount` are also read from UDP and remain **mappable**,
but they are **no longer in the default mapping**: GPS is sent losslessly via NMEA
`$GPRMC`/`$GPGGA`, and RaceChrono computes lap timing itself.

**From shared memory (requires `SameMachineWithAC`):**

| Channel | Meaning | Unit | Source field |
| --- | --- | --- | --- |
| `SteerAngle` | steering (left −, right +) | normalized, ~±1 at full lock | `Physics.SteerAngle` |
| `Fuel` | fuel level | litres | `Physics.Fuel` |
| `FuelPercent` | fuel remaining | % | `Physics.Fuel` / `StaticInfo.MaxFuel` |
| `TyreTempFL` / `TyreTempFR` / `TyreTempRL` / `TyreTempRR` | tyre core temps | °C | `Physics.TyreCoreTemperature[0..3]` |
| `TyrePressureFL` / `TyrePressureFR` / `TyrePressureRL` / `TyrePressureRR` | tyre pressures | psi | `Physics.WheelsPressure[0..3]` |
| `BrakeTempFL` / `BrakeTempFR` / `BrakeTempRL` / `BrakeTempRR` | brake temps | °C | `Physics.BrakeTemp[0..3]` |
| `BrakeTempMax` | hottest brake | °C | max of the four |
| `Heading` / `Pitch` / `Roll` | attitude | ° | `Physics.Heading` / `Physics.Pitch` / `Physics.Roll` |
| (gyro) | angular velocity → RC3 `gyrox`/`gyroy`/`gyroz` | deg/s | `Physics.LocalAngularVelocity[0..2]` |

> Assetto Corsa shared-memory struct layout adapted from
> [mdjarv/assettocorsasharedmemory](https://github.com/mdjarv/assettocorsasharedmemory)
> under the MIT License.

Notes:

- Acceleration axes remain fixed (not remappable): `xacc=AccGHorizontal` (lateral),
  `yacc=AccGVertical`, `zacc=AccGFrontal`, all in G. The `gyrox`/`gyroy`/`gyroz` fields,
  previously always 0, are now filled from shared-memory angular velocity
  (`Physics.LocalAngularVelocity`, rad/s → deg/s) when `SameMachineWithAC` is on. This
  feeds RaceChrono's IMU fusion (orientation / lean angle); it does **not** change the
  NMEA sentences. Verified against live AC: `LocalAngularVelocity` `[0]`=pitch,
  `[1]`=yaw, `[2]`=roll — matching the `x`=lateral / `y`=vertical / `z`=longitudinal
  order (a left turn gives +`gyroy`); `Heading`/`Pitch`/`Roll` are radians.
- **Not provided by Assetto Corsa at all:** engine coolant/water temperature, oil
  temperature, and oil pressure are NOT provided by Assetto Corsa via UDP or shared
  memory, so they cannot be bridged.
- **Packing:** RaceChrono's equation engine (bit/byte extraction) applies only to
  OBD-II / CAN-Bus channels, NOT to RC3 line-protocol analog channels — so each RC3
  analog slot carries a single value (no bit-packing). Because there are only 15 analog
  slots, per-corner brake temps are not in the default map (`BrakeTempMax` is used
  instead); full per-corner data would require the CAN-Bus BLE transport.
- `$RC3` analog/digital fields are printed with 3 decimal places, so `Latitude`/`Longitude`
  placed in analog slots are low-precision.

Available in AC UDP but not yet bridged (would require extending the reader): per-wheel
wheel speeds, slip ratio / tyre slip, tyre load, suspension height, `CamberRad`, and
ABS/TC "in action" flags. (Steering *is* bridged — but from shared memory as
`SteerAngle`, so it needs `SameMachineWithAC`; AC's UDP `Steer` field is not used.)

### RC3 channel mapping
The slot→channel mapping is configurable in `appsettings.json` under `Bridge:Rc3Channels`.
Mappable slots are `d1`, `d2`, and `a1`..`a15`. Each value must be one of the `Channel`
names from the tables above — including the now-non-default GPS/lap-timing channels —
and any channel can be mapped to any `d1`/`d2`/`a1`..`a15` slot. `None` (or omitting the
slot) outputs a constant 0. An unknown slot or channel name fails fast at startup.
Omitting `Rc3Channels` entirely keeps the defaults shown.

Shared-memory channels (`SteerAngle`, `Fuel`, tyre/brake temps and pressures, attitude,
gyro, …) output 0 unless `Games:AssettoCorsa:SameMachineWithAC` is `true` and Assetto
Corsa is running on the same machine.

```json
"Games": {
  "AssettoCorsa": { "SameMachineWithAC": true }
},
"Bridge": {
  "Port": 19996,
  "BindAddress": "0.0.0.0",
  "Rc3Channels": {
    "d1": "EngineRpm", "d2": "Gear",
    "a1": "SpeedKmh", "a2": "Gas", "a3": "Brake", "a4": "Clutch",
    "a5": "SteerAngle", "a6": "Fuel",
    "a7": "TyreTempFL", "a8": "TyreTempFR", "a9": "TyreTempRL", "a10": "TyreTempRR",
    "a11": "TyrePressureFL", "a12": "TyrePressureFR", "a13": "TyrePressureRL", "a14": "TyrePressureRR",
    "a15": "BrakeTempMax"
  }
}
```

### Useful links
- [RaceChrono forum thread "Assetto Corsa?"](https://racechrono.com/forum/discussion/1892/assetto-corsa)
- [RaceChrono article "Tutorial: DIY devices"](https://racechrono.com/article/2572)
- [AC Socket Document](https://docs.google.com/document/d/1KfkZiIluXZ6mMhLWfDX1qAGbvhGRC3ZUzjVIt5FQpp4/pub)
- [AC forum thread "AC UDP Remote Telemetry"](https://www.assettocorsa.net/forum/index.php?threads/ac-udp-remote-telemetry-update-31-03-2016.222/)
- [Assetto Corsa Remote Telemetry Client](https://github.com/rickwest/ac-remote-telemetry-client) by [@rickwest](https://github.com/rickwest)
- [mdjarv/assettocorsasharedmemory](https://github.com/mdjarv/assettocorsasharedmemory) — Assetto Corsa shared-memory struct layout adapted under the MIT License.
- [Topograhic maps (useful to find height of a location)](https://topographic-map.com/)

### TODO
- Wiki/Instruction how to use with screenshots
- [Issues tab](https://github.com/dlinov/acrc-bridge/issues)

### CI
The workflow is in `.github/workflows/ci.yml` (named **CI** in Actions).

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
