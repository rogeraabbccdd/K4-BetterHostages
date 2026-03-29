// Edited from: https://github.com/Cruze03/FortniteEmotesNDances/blob/main/Plugin/RayTrace.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Memory;
using RayTraceAPI;
using System.Runtime.InteropServices;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace HostageRescue;

public static class RayTraceBridge
{
    public static nint m_pRayTraceHandle = nint.Zero;
    public static bool m_bRayTraceLoaded = false;

    public static Func<nint, nint, nint, nint, nint, nint, bool>? _traceShape;
    public static Func<nint, nint, nint, nint, nint, nint, bool>? _traceEndShape;
    public static Func<nint, nint, nint, nint, nint, nint, nint, nint, bool>? _traceHullShape;

    public static bool Initialize()
    {
        m_pRayTraceHandle = (nint)Utilities.MetaFactory("CRayTraceInterface001")!;

        if (m_pRayTraceHandle == nint.Zero)
        {
            return false;
        }

        Bind();
        m_bRayTraceLoaded = true;
        return true;
    }

    private static void Bind()
    {
        int traceShapeIndex = 2;
        int traceEndShapeIndex = 3;
        int traceHullShapeIndex = 4;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            traceShapeIndex = 1;
            traceEndShapeIndex = 2;
            traceHullShapeIndex = 3;
        }

        _traceShape = VirtualFunction.Create<nint, IntPtr, IntPtr, nint, nint, nint, bool>(m_pRayTraceHandle, traceShapeIndex);
        _traceEndShape = VirtualFunction.Create<nint, IntPtr, IntPtr, nint, nint, nint, bool>(m_pRayTraceHandle, traceEndShapeIndex);
        _traceHullShape = VirtualFunction.Create<nint, IntPtr, IntPtr, IntPtr, IntPtr, nint, nint, nint, bool>(m_pRayTraceHandle, traceHullShapeIndex);
    }
}

public class CRayTrace : CRayTraceInterface
{
    public unsafe bool TraceShape(Vector origin, QAngle angles, CBaseEntity? ignoreEntity, TraceOptions options, out TraceResult result)
    {
        result = default;

        if (!RayTraceBridge.m_bRayTraceLoaded || RayTraceBridge.m_pRayTraceHandle == nint.Zero)
            return false;

        TraceResult resultBuffer = default;
        TraceOptions optionsBuffer = options;

        bool success = RayTraceBridge._traceShape!(RayTraceBridge.m_pRayTraceHandle,
            origin.Handle,
            angles.Handle,
            ignoreEntity?.Handle ?? nint.Zero,
            (nint)(&optionsBuffer),
            (nint)(&resultBuffer));

        result = resultBuffer;
        return success;
    }

    public unsafe bool TraceEndShape(Vector origin, Vector endOrigin, CBaseEntity? ignoreEntity, TraceOptions options, out TraceResult result)
    {
        result = default;

        if (!RayTraceBridge.m_bRayTraceLoaded || RayTraceBridge.m_pRayTraceHandle == nint.Zero)
            return false;

        TraceResult resultBuffer = default;
        TraceOptions optionsBuffer = options;

        bool success = RayTraceBridge._traceEndShape!(RayTraceBridge.m_pRayTraceHandle,
            origin.Handle,
            endOrigin.Handle,
            ignoreEntity?.Handle ?? nint.Zero,
            (nint)(&optionsBuffer),
            (nint)(&resultBuffer));

        result = resultBuffer;
        return success;
    }

    public unsafe bool TraceHullShape(Vector vecStart, Vector vecEnd, Vector hullMins, Vector hullMaxs, CBaseEntity? ignoreEntity, TraceOptions options, out TraceResult result)
    {
        result = default;

        if (!RayTraceBridge.m_bRayTraceLoaded || RayTraceBridge.m_pRayTraceHandle == nint.Zero)
            return false;

        TraceResult resultBuffer = default;
        TraceOptions optionsBuffer = options;

        bool success = RayTraceBridge._traceHullShape!(RayTraceBridge.m_pRayTraceHandle,
            vecStart.Handle,
            vecEnd.Handle,
            hullMins.Handle,
            hullMaxs.Handle,
            ignoreEntity?.Handle ?? nint.Zero,
            (nint)(&optionsBuffer),
            (nint)(&resultBuffer));

        result = resultBuffer;
        return success;
    }
}