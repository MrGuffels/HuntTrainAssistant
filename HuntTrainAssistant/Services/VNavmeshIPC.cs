using ECommons.EzIpcManager;
using System;
using System.Numerics;

namespace HuntTrainAssistant.Services;
public class VNavmeshIPC
{
    private VNavmeshIPC()
    {
        EzIPC.Init(this, "vnavmesh", SafeWrapper.AnyException);
    }

    [EzIPC("Nav.IsReady")] public readonly Func<bool> IsReady;
    [EzIPC("Nav.PathfindInProgress")] public readonly Func<bool> NavPathfindInProgress;
    [EzIPC("SimpleMove.PathfindInProgress")] public readonly Func<bool> SimpleMovePathfindInProgress;
    [EzIPC("Path.IsRunning")] public readonly Func<bool> IsRunning;
    [EzIPC("SimpleMove.PathfindAndMoveTo")] public readonly Func<Vector3, bool, bool> PathfindAndMoveTo;
    [EzIPC("Path.Stop")] public readonly Action Stop;

    /// <summary>
    ///     Whether vnavmesh is currently computing a route. A new PathfindAndMoveTo call issued while
    ///     this is true would either get rejected or replace the in-flight route mid-computation --
    ///     the latter is what caused the jerky backtrack, since the new route starts from wherever the
    ///     player was when the (now-discarded) old computation began, not where they've since moved to.
    /// </summary>
    public bool IsPathfinding() => SimpleMovePathfindInProgress() || NavPathfindInProgress();

    /// <returns>False (and does nothing) if a route is already being computed -- caller should retry later.</returns>
    public bool TryMoveTo(Vector3 destination, bool fly)
    {
        if(IsPathfinding()) return false;
        return PathfindAndMoveTo(destination, fly);
    }
}
