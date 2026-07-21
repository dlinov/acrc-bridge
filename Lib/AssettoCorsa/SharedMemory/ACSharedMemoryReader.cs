using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ACRCBridge.Lib.Dto;

namespace ACRCBridge.Lib.AssettoCorsa.SharedMemory;

/// <summary>
/// Reads Assetto Corsa's shared-memory physics/static blocks and exposes the extra channels
/// that are not available over UDP. Assetto Corsa publishes shared memory only on Windows and
/// only on the machine it runs on, so <see cref="TryGetSnapshot"/> returns <c>false</c> until
/// the memory-mapped files exist (and always on non-Windows platforms).
/// </summary>
internal sealed class ACSharedMemoryReader : IPhysicsSnapshotSource, IDisposable
{
    private const string PhysicsMapName = @"Local\acpmf_physics";
    private const string StaticMapName = @"Local\acpmf_static";
    private const float RadToDeg = 180f / MathF.PI;

    private static readonly int PhysicsSize = Marshal.SizeOf<Physics>();
    private static readonly int StaticSize = Marshal.SizeOf<StaticInfo>();

    private readonly object _lock = new();
    private readonly byte[] _physicsBuffer = new byte[PhysicsSize];

    private MemoryMappedFile? _physicsFile;
    private MemoryMappedViewAccessor? _physicsAccessor;
    private float _maxFuel;
    private bool _staticLoaded;
    private bool _disposed;

    public bool TryGetSnapshot(out AcPhysicsExtras extras)
    {
        extras = default;

        // Assetto Corsa only publishes shared memory on Windows; also keeps CA1416 satisfied.
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return false;
            }

            if (!TryEnsurePhysicsOpen())
            {
                return false;
            }

            Physics physics;
            try
            {
                _physicsAccessor!.ReadArray(0, _physicsBuffer, 0, PhysicsSize);
                physics = MarshalStruct<Physics>(_physicsBuffer);
            }
            catch (Exception)
            {
                // The view becomes invalid when Assetto Corsa exits; drop it and retry on a later call.
                ClosePhysics();
                return false;
            }

            TryEnsureStaticLoaded();

            extras = BuildExtras(in physics, _maxFuel);
            return true;
        }
    }

    [SupportedOSPlatform("windows")]
    private bool TryEnsurePhysicsOpen()
    {
        if (_physicsAccessor is not null)
        {
            return true;
        }

        try
        {
            _physicsFile = MemoryMappedFile.OpenExisting(PhysicsMapName, MemoryMappedFileRights.Read);
            _physicsAccessor = _physicsFile.CreateViewAccessor(0, PhysicsSize, MemoryMappedFileAccess.Read);
            return true;
        }
        catch (Exception)
        {
            // Assetto Corsa is not running or not on this machine. Clean up and retry later.
            ClosePhysics();
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private void TryEnsureStaticLoaded()
    {
        if (_staticLoaded)
        {
            return;
        }

        try
        {
            using var staticFile = MemoryMappedFile.OpenExisting(StaticMapName, MemoryMappedFileRights.Read);
            using var staticAccessor = staticFile.CreateViewAccessor(0, StaticSize, MemoryMappedFileAccess.Read);
            var buffer = new byte[StaticSize];
            staticAccessor.ReadArray(0, buffer, 0, StaticSize);
            var staticInfo = MarshalStruct<StaticInfo>(buffer);
            _maxFuel = staticInfo.MaxFuel;
            _staticLoaded = true;
        }
        catch (Exception)
        {
            // Static block not published yet; FuelPercent stays 0 until it can be read.
        }
    }

    private static AcPhysicsExtras BuildExtras(in Physics p, float maxFuel)
    {
        // Wheel/tyre/brake arrays are ordered FL=0, FR=1, RL=2, RR=3.
        var tyreTemp = p.TyreCoreTemperature;
        var pressure = p.WheelsPressure;
        var brakeTemp = p.BrakeTemp;

        var brakeTempMax = MathF.Max(
            MathF.Max(brakeTemp[0], brakeTemp[1]),
            MathF.Max(brakeTemp[2], brakeTemp[3]));

        var fuelPercent = maxFuel > 0f ? p.Fuel / maxFuel * 100f : 0f;

        // Verified 2026-07-21 against live AC: LocalAngularVelocity is rad/s in the car frame,
        // with [0]=pitch (lateral axis), [1]=yaw (vertical axis), [2]=roll (longitudinal axis).
        // That matches the RC3 acc convention (x=lateral, y=vertical, z=longitudinal), so the
        // index order maps straight through: [0]->gyrox, [1]->gyroy, [2]->gyroz. A left turn
        // produces +gyroy (yaw). Flip a sign here only if RaceChrono shows mirrored orientation.
        var angularVelocity = p.LocalAngularVelocity;
        var gyroX = angularVelocity[0] * RadToDeg;
        var gyroY = angularVelocity[1] * RadToDeg;
        var gyroZ = angularVelocity[2] * RadToDeg;

        return new AcPhysicsExtras(
            SteerAngle: p.SteerAngle,
            Fuel: p.Fuel,
            FuelPercent: fuelPercent,
            TyreTempFL: tyreTemp[0],
            TyreTempFR: tyreTemp[1],
            TyreTempRL: tyreTemp[2],
            TyreTempRR: tyreTemp[3],
            TyrePressureFL: pressure[0],
            TyrePressureFR: pressure[1],
            TyrePressureRL: pressure[2],
            TyrePressureRR: pressure[3],
            BrakeTempFL: brakeTemp[0],
            BrakeTempFR: brakeTemp[1],
            BrakeTempRL: brakeTemp[2],
            BrakeTempRR: brakeTemp[3],
            BrakeTempMax: brakeTempMax,
            GyroXDegPerSec: gyroX,
            GyroYDegPerSec: gyroY,
            GyroZDegPerSec: gyroZ,
            // Verified 2026-07-21 against live AC: Heading/Pitch/Roll are radians
            // (heading swept ~-pi..pi while cornering), so convert to degrees.
            HeadingDeg: p.Heading * RadToDeg,
            PitchDeg: p.Pitch * RadToDeg,
            RollDeg: p.Roll * RadToDeg);
    }

    private static T MarshalStruct<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicConstructors)]
        T>(byte[] buffer) where T : struct
    {
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private void ClosePhysics()
    {
        _physicsAccessor?.Dispose();
        _physicsAccessor = null;
        _physicsFile?.Dispose();
        _physicsFile = null;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ClosePhysics();
        }
    }
}
