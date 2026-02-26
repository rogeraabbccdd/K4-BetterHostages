using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace HostageRescue;

public partial class HostageRescuePlugin
{
    private void OnPlayerButtonsChanged(CCSPlayerController player, PlayerButtons pressed, PlayerButtons released)
    {
        if (!player.IsValid || player.UserId == null)
            return;

        var warmup = Utilities
                .FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                .First().GameRules?.WarmupPeriod;
        if (warmup == true)
            return;

        if (pressed == PlayerButtons.Use)
            StartHostageAction(player);
        else if (released == PlayerButtons.Use)
            CancelHostageAction(player.UserId.Value);
    }

    private void StartHostageAction(CCSPlayerController player)
    {
        var playerId = (int)player.UserId!;
        var pawn = player.PlayerPawn.Value;

        if (pawn == null)
            return;

        CancelHostageAction((int)player.UserId!);

        // already carrying? drop it
        if (pawn.HostageServices?.CarriedHostage.Value is CBaseEntity carriedHostage && carriedHostage.IsValid)
        {
            var state = new ActionState { Type = ActionType.DroppingHostage };
            _playerActionState[playerId] = state;

            SetProgressBar(player, DROP_DURATION, CSPlayerBlockingUseAction_t.k_CSPlayerBlockingUseAction_HostageDropping);
            PlaySound(_hostageDropSound, pawn);

            var progressTimer = AddTimer(DROP_DURATION, () => CompleteDropHostage(player));
            _playerProgressTimers[playerId] = progressTimer;

            var validationTimer = AddTimer(0.25f, () => ValidateDropHostageAction(player), TimerFlags.REPEAT);
            _playerValidationTimers[playerId] = validationTimer;
        }
        else
        {
            // only Ts can grab hostages
            if (player.Team != CsTeam.Terrorist)
                return;

            var hostage = GetHostageInView(player);
            if (hostage?.IsValid != true)
                return;

            if (pawn.AbsOrigin == null || hostage.AbsOrigin == null)
                return;
            var distance = (pawn.AbsOrigin - hostage.AbsOrigin).Length();
            if (distance > PICKUP_RANGE)
                return;

            if (hostage.Leader.Value != null || hostage.IsRescued)
                return;

            var state = new ActionState { Type = ActionType.PickingUpHostage, TargetHostage = hostage };
            _playerActionState[playerId] = state;

            SetProgressBar(player, PICKUP_DURATION, CSPlayerBlockingUseAction_t.k_CSPlayerBlockingUseAction_HostageGrabbing);
            PlaySound(_hostagePickupSound, hostage);

            var progressTimer = AddTimer(PICKUP_DURATION, () => CompletePickupHostage(player, hostage));
            _playerProgressTimers[playerId] = progressTimer;

            var validationTimer = AddTimer(0.25f, () => ValidatePickupHostageAction(player, hostage), TimerFlags.REPEAT);
            _playerValidationTimers[playerId] = validationTimer;
        }
    }

    private void CancelHostageAction(int playerId)
    {
        bool hadActiveAction = _playerActionState.ContainsKey(playerId);

        if (_playerProgressTimers.TryGetValue(playerId, out var progressTimer))
        {
            progressTimer.Kill();
            _playerProgressTimers.Remove(playerId);
        }

        if (_playerValidationTimers.TryGetValue(playerId, out var validationTimer))
        {
            validationTimer.Kill();
            _playerValidationTimers.Remove(playerId);
        }

        _playerActionState.Remove(playerId);

        if (hadActiveAction)
        {
            var player = Utilities.GetPlayerFromUserid(playerId);
            if (player?.PlayerPawn?.IsValid == true)
                SetProgressBar(player, 0, CSPlayerBlockingUseAction_t.k_CSPlayerBlockingUseAction_None);
        }
    }

    private void ValidatePickupHostageAction(CCSPlayerController player, CHostage hostage)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn?.AbsOrigin == null || hostage?.IsValid != true || hostage.AbsOrigin == null)
        {
            CancelHostageAction((int)player.UserId!);
            return;
        }

        float distance = (pawn.AbsOrigin - hostage.AbsOrigin).Length();
        if (distance >= PICKUP_RANGE)
        {
            CancelHostageAction((int)player.UserId!);
            return;
        }

        var inView = GetHostageInView(player);
        if (inView?.Index != hostage.Index)
            CancelHostageAction((int)player.UserId!);
    }

    private void ValidateDropHostageAction(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn?.IsValid != true)
        {
            CancelHostageAction((int)player.UserId!);
            return;
        }

        if (pawn.HostageServices?.CarriedHostage.Value?.IsValid != true)
            CancelHostageAction((int)player.UserId!);
    }

    private void CompletePickupHostage(CCSPlayerController player, CHostage hostage)
    {
        CancelHostageAction((int)player.UserId!);

        if (hostage?.IsValid != true)
            return;

        hostage.HostageState = 2;
        Utilities.SetStateChanged(hostage, "CHostage", "m_nHostageState");

        hostage.GrabSuccessTime = Server.CurrentTime;
        Utilities.SetStateChanged(hostage, "CHostage", "m_flGrabSuccessTime");

        if (hostage.AbsOrigin != null)
        {
            hostage.GrabbedPos.X = hostage.AbsOrigin.X;
            hostage.GrabbedPos.Y = hostage.AbsOrigin.Y;
            hostage.GrabbedPos.Z = hostage.AbsOrigin.Z;
        }

        // small delay before actually attaching to player
        AddTimer(0.25f, () =>
        {
            if (hostage?.IsValid != true)
                return;

            hostage.HostageState = 3;
            Utilities.SetStateChanged(hostage, "CHostage", "m_nHostageState");

            hostage.Effects |= 32; // invisibility
            Utilities.SetStateChanged(hostage, "CBaseEntity", "m_fEffects");

            InvokeHostageFollow(player, hostage);
            SetProgressBar(player, 0, CSPlayerBlockingUseAction_t.k_CSPlayerBlockingUseAction_None);
        });
    }

    private void CompleteDropHostage(CCSPlayerController player)
    {
        CancelHostageAction((int)player.UserId!);

        var pawn = player.PlayerPawn.Value;
        if (pawn?.AbsOrigin != null)
        {
            var dropPos = GetDropPosition(pawn);
            InvokeHostageDrop(player, dropPos);
        }
    }
}
