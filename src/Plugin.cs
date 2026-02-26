using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using RayTraceAPI;

namespace HostageRescue;

public partial class HostageRescuePlugin : BasePlugin
{
    public override string ModuleName => "K4 - Better Hostages";
	public override string ModuleVersion => "1.0.1";
	public override string ModuleAuthor => "K4ryuu";
	public override string ModuleDescription => "Enables both Ts and CTs to pick up and drop hostages, allowing their positions to be tactically rearranged during gameplay.";

    public const float PICKUP_RANGE = 62.0f;
    public const float PICKUP_DURATION = 1.0f;
    public const float DROP_DURATION = 1.0f;
    public const float MINIMUM_SAFE_DISTANCE = 35f;

    internal MemoryFunctionVoid<nint, nint> _cHostageFollow = new(GameData.GetSignature("CHostage::Follow"));
    internal MemoryFunctionVoid<nint, Vector, bool> _cHostageDrop = new(GameData.GetSignature("CHostage::DropHostage"));

    internal readonly Dictionary<int, CounterStrikeSharp.API.Modules.Timers.Timer> _playerProgressTimers = [];
    internal readonly Dictionary<int, CounterStrikeSharp.API.Modules.Timers.Timer> _playerValidationTimers = [];
    internal readonly Dictionary<int, ActionState> _playerActionState = [];

    internal readonly string _hostagePickupSound = "Hostage.CutFreeWithDefuser";
    internal readonly string _hostageDropSound = "Hostage.CutFreeWithDefuser";
    
    private readonly PluginCapability<CRayTraceInterface> _rayTraceCapability = new("raytrace:craytraceinterface");
    private CRayTraceInterface? rayTrace;

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnPlayerButtonsChanged>(OnPlayerButtonsChanged);
    }

    public override void Unload(bool hotReload)
    {
        foreach (var timer in _playerProgressTimers.Values)
            timer.Kill();

        foreach (var timer in _playerValidationTimers.Values)
            timer.Kill();

        _playerProgressTimers.Clear();
        _playerValidationTimers.Clear();
        _playerActionState.Clear();
    }
}
