using CounterStrikeSharp.API.Core;

namespace HostageRescue;

internal class ActionState
{
    public ActionType Type { get; set; }
    public CHostage? TargetHostage { get; set; }
}

internal enum ActionType
{
    PickingUpHostage,
    DroppingHostage
}
