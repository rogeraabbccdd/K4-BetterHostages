using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace HostageRescue;

public partial class HostageRescuePlugin
{
    private CHostage? GetHostageInView(CCSPlayerController player)
    {
        var gameRules = Utilities
                .FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                .First().GameRules;

        if (gameRules == null)
            return null;
                
        var entity = gameRules.FindPickerEntity<CHostage>(player);

        if (entity?.IsValid == true && entity.DesignerName == "hostage_entity")
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn?.AbsOrigin != null && entity?.AbsOrigin != null)
            {
                var distance = GetDistance(pawn.AbsOrigin, entity.AbsOrigin);
                if (distance <= PICKUP_RANGE)
                    return entity;
            }
        }

        return null;
    }

    private Vector GetDropPosition(CCSPlayerPawn pawn)
    {
        var playerPos = pawn.AbsOrigin ?? Vector.Zero;
        var playerRotation = pawn.AbsRotation ?? QAngle.Zero;

        // try dropping in front first
        float playerYaw = playerRotation.Y * (float)(Math.PI / 180.0f);
        float radianY = playerYaw * (MathF.PI / 180f);
        var forward = new Vector(
            MathF.Cos(radianY) * PICKUP_RANGE,
            MathF.Sin(radianY) * PICKUP_RANGE,
            0
        );
        var initialPosition = playerPos + forward;

        if (IsValidHostagePosition(pawn, initialPosition))
            return initialPosition;

        return FindValidHostagePosition(pawn, initialPosition);
    }

    private static float GetDistance(Vector pos1, Vector pos2) => (pos1 - pos2).Length();

    private Vector FindValidHostagePosition(CCSPlayerPawn pawn, Vector initialPosition)
    {
        var playerPos = pawn.AbsOrigin ?? Vector.Zero;
        var playerRotation = pawn.AbsRotation ?? QAngle.Zero;

        if (IsValidHostagePosition(pawn, initialPosition))
            return initialPosition;

        float originalDropDistance = GetDistance(playerPos, initialPosition);
        float searchDistance = Math.Max(originalDropDistance, MINIMUM_SAFE_DISTANCE);
        float playerYaw = playerRotation.Y * (float)(Math.PI / 180.0f);

        // try sides first, then diagonals, then behind
        int[] smartAngles = [-90, 90, -60, 60, -120, 120, -30, 30, 180, 0];

        foreach (int relativeAngle in smartAngles)
        {
            float absoluteAngle = playerYaw + relativeAngle;
            float radians = absoluteAngle * (MathF.PI / 180f);
            var testPosition = new Vector(
                playerPos.X + MathF.Cos(radians) * searchDistance,
                playerPos.Y + MathF.Sin(radians) * searchDistance,
                playerPos.Z
            );

            if (IsValidHostagePosition(pawn, testPosition))
                return testPosition;
        }

        // fallback: just drop behind
        float backwardAngle = (playerYaw + 180) * (MathF.PI / 180f);
        return new Vector(
            playerPos.X + MathF.Cos(backwardAngle) * MINIMUM_SAFE_DISTANCE,
            playerPos.Y + MathF.Sin(backwardAngle) * MINIMUM_SAFE_DISTANCE,
            playerPos.Z
        );
    }

    private bool IsValidHostagePosition(CCSPlayerPawn playerPawn, Vector position)
    {
        rayTrace = _rayTraceCapability.Get();
        if (rayTrace == null)
            return false;

        var playerPos = playerPawn.AbsOrigin ?? Vector.Zero;

        if (GetDistance(position, playerPos) < 25f)
            return false;

        var allPlayers = Utilities.GetPlayers();
        if (allPlayers != null)
        {
            foreach (var otherPlayer in allPlayers)
            {
                if (otherPlayer?.PlayerPawn?.Value?.AbsOrigin == null)
                    continue;

                var ownerEntity = playerPawn.OwnerEntity;
                if (ownerEntity.IsValid && otherPlayer.UserId == ownerEntity.Index)
                    continue;

                var otherPos = otherPlayer.PlayerPawn.Value.AbsOrigin;
                if (otherPos != null && GetDistance(position, otherPos) < 25f)
                    return false;
            }
        }

        var endPos = new Vector(
            position.X + (position.X - playerPos.X) * 0.3f,
            position.Y + (position.Y - playerPos.Y) * 0.3f,
            position.Z + 10
        );
        
        var maskPlayerSolid = RayTraceAPI.InteractionLayers.Solid | RayTraceAPI.InteractionLayers.Player | RayTraceAPI.InteractionLayers.NPC | RayTraceAPI.InteractionLayers.WorldGeometry | RayTraceAPI.InteractionLayers.Physics_Prop | RayTraceAPI.InteractionLayers.StaticLevel;

        RayTraceAPI.TraceOptions options = new()
        {
            InteractsAs = (ulong)RayTraceAPI.InteractionLayers.Player,
            InteractsExclude = 0,
            InteractsWith = (ulong)maskPlayerSolid
        };
        rayTrace.TraceHullShape(position, endPos, new Vector(-16, -16, -0), new Vector(16, 16, 72), null, options, out var trace);
        
        return trace.Fraction > 0.5f;
    }

    private void InvokeHostageFollow(CCSPlayerController player, CHostage hostage)
    {
        if (_cHostageFollow == null)
            return;

        var pawn = player.PlayerPawn.Value;
        if (pawn?.IsValid != true || hostage?.IsValid != true)
            return;

        _cHostageFollow.Invoke(hostage.Handle, pawn.Handle);
    }

    private void InvokeHostageDrop(CCSPlayerController player, Vector? dropPosition = null)
    {
        if (_cHostageDrop == null)
            return;

        var pawn = player.PlayerPawn;
        if (pawn.Value == null && pawn?.IsValid != true)
            return;

        var carriedHostage = pawn.Value?.HostageServices?.CarriedHostage.Value;
        if (carriedHostage?.IsValid != true)
            return;

        var dropPos = dropPosition ?? pawn.Value?.AbsOrigin ?? new Vector(0, 0, 0);
        var hostageHandle = carriedHostage.Handle;

        if (hostageHandle == IntPtr.Zero)
            return;

        _cHostageDrop.Invoke(hostageHandle, dropPos, false);
    }

    private void SetProgressBar(CCSPlayerController player, float duration, CSPlayerBlockingUseAction_t actionType)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null && pawn?.IsValid != true)
            return;

        int progressTime = (int)Math.Ceiling(duration);
        float currentTime = Server.CurrentTime;
        pawn.ProgressBarDuration = progressTime;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawnBase", "m_iProgressBarDuration");

        pawn.ProgressBarStartTime = duration == 0 ? 0 : currentTime;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawnBase", "m_flProgressBarStartTime");

        pawn.SimulationTime = currentTime + duration;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_flSimulationTime");

        pawn.BlockingUseActionInProgress = actionType;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_iBlockingUseActionInProgress");
    }

    private void PlaySound(string sound, CBaseEntity source)
    {
        RecipientFilter filter = new RecipientFilter();
        filter.AddAllPlayers();
        source.EmitSound(sound, filter, 1f);
    }
}
