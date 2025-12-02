//Taken and adapted from https://github.com/awgil/ffxiv_navmesh/blob/master/vnavmesh/Movement/OverrideCamera.cs.

namespace Questionable.Functions;

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Microsoft.Extensions.Logging;

[StructLayout(LayoutKind.Explicit, Size = 0x2B0)]
internal unsafe struct CameraEx
{
    [FieldOffset(0x140)] public float DirH; // 0 is north, increases CW
    [FieldOffset(0x144)] public float DirV; // 0 is horizontal, positive is looking up, negative looking down
    [FieldOffset(0x148)] public float InputDeltaHAdjusted;
    [FieldOffset(0x14C)] public float InputDeltaVAdjusted;
    [FieldOffset(0x150)] public float InputDeltaH;
    [FieldOffset(0x154)] public float InputDeltaV;
    [FieldOffset(0x158)] public float DirVMin; // -85deg by default
    [FieldOffset(0x15C)] public float DirVMax; // +45deg by default
}

internal sealed unsafe class CameraFunctions : IDisposable
{
    private readonly ILogger<CameraFunctions> _logger;
    private readonly IObjectTable _objectTable;

    public bool Enabled
    {
        get => _rmiCameraHook.IsEnabled;
        set
        {
            if (value)
                _rmiCameraHook.Enable();
            else
                _rmiCameraHook.Disable();
        }
    }

    private readonly bool IgnoreUserInput = true; // if true - override even if user tries to change camera orientation, otherwise override only if user does nothing
    private float DesiredAzimuth;
    private float DesiredAltitude;

    private delegate void RMICameraDelegate(CameraEx* self, int inputMode, float speedH, float speedV);
    [Signature("48 8B C4 53 48 81 EC ?? ?? ?? ?? 44 0F 29 50 ??")]
    private Hook<RMICameraDelegate> _rmiCameraHook = null!;

    public CameraFunctions(IGameInteropProvider gameInteropProvider, ILogger<CameraFunctions> logger, IObjectTable objectTable)
    {
        _logger = logger;
        gameInteropProvider.InitializeFromAttributes(this);
        _objectTable = objectTable;
    }

    public void Dispose()
    {
        _rmiCameraHook.Dispose();
    }

    private static float Deg2Rad(int degrees)
    {
        return degrees * ((float)Math.PI / 180f);
    }

    // from https://github.com/NightmareXIV/ECommons/blob/master/ECommons/MathHelpers/Angle.cs
    private static float Normalized(float r)
    {
        while (r < -MathF.PI)
            r += 2 * MathF.PI;
        while (r > MathF.PI)
            r -= 2 * MathF.PI;
        return r;
    }


    internal void Face(Vector3 pos)
    {
        _logger.LogDebug("Facing " + pos);
        Enabled = true;
        if (_objectTable.LocalPlayer == null)
        {
            return;
        }
        Vector3 diff = pos - _objectTable.LocalPlayer.Position;
        DesiredAzimuth = MathF.Atan2(diff.X, diff.Z) + Deg2Rad(180);
        DesiredAltitude = Deg2Rad(-30);
    }

    private void RMICameraDetour(CameraEx* self, int inputMode, float speedH, float speedV)
    {
        _rmiCameraHook.Original(self, inputMode, speedH, speedV);
        if (IgnoreUserInput || inputMode == 0) // let user override...
        {
            var dt = Framework.Instance()->FrameDeltaTime;
            var deltaH = Normalized(DesiredAzimuth - self->DirH);
            var deltaV = Normalized(DesiredAltitude - self->DirV);
            var maxH = Deg2Rad(180);
            var maxV = Deg2Rad(180);
            self->InputDeltaH = Math.Clamp(deltaH, -maxH, maxH);
            self->InputDeltaV = Math.Clamp(deltaV, -maxV, maxV);
            Enabled = false;
        }
    }
}