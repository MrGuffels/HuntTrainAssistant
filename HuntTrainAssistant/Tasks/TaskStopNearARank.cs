using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System.Linq;

namespace HuntTrainAssistant.Tasks;
public static class TaskStopNearARank
{
    /// <summary>
    ///     Last position a flyto was actually issued for. Re-pathing on every throttle tick regardless
    ///     of whether the mob has moved re-triggers vnavmesh's computation (0.5s+ for anything but a
    ///     trivial route) while the player keeps flying the old path in the meantime -- so the new path
    ///     lands starting from a position the player has since flown past, causing a visible backtrack.
    ///     Only reissuing once the mob has actually drifted keeps that recompute rare instead of a
    ///     fixed-timer habit.
    /// </summary>
    private static Vector3? _lastCommandedTarget;

    private const int LosSearchPositions = 8;
    private const float LosSearchRadius = 5f;
    private static Vector3? _losMoveTarget;
    private static int _losSearchAttempt;

    /// <summary>
    ///     The flagged destination, so the live-mob search below only considers A-ranks actually near
    ///     it -- otherwise an unrelated A-rank that happens to be closer to the player (e.g. one Sonar
    ///     just reported elsewhere in the zone, still on cooldown/being saved for later) hijacks the
    ///     flight instead of the one that was actually flagged.
    /// </summary>
    private const float FlagSearchRadius = 30f;
    private static Vector3? _flagPos;

    public static void EnqueueIfEnabled(Vector3? flagPos = null)
    {
        if(P.Config.StopNearARankEnabled)
        {
            _lastCommandedTarget = null;
            _losMoveTarget = null;
            _losSearchAttempt = 0;
            _flagPos = flagPos;
            P.TaskManager.Enqueue(WaitUntilNearARank, "Wait until near A-rank", new(timeLimitMS: 120000));
            P.TaskManager.Enqueue(TargetNearestARank, "Target A-rank", new(timeLimitMS: 15000));
            P.TaskManager.Enqueue(StopAndDismount, "Stop and dismount", new(timeLimitMS: 15000));
            P.TaskManager.Enqueue(EnsureLineOfSight, "Ensure line of sight", new(timeLimitMS: 20000));
        }
    }

    /// <summary>
    ///     While the flag-chase is still en route, redirect toward the A-rank's live position once it's
    ///     targetable -- the flag can be stale/off from where the mob actually spawned or wandered to.
    ///     Distance is measured past the mob's hitbox edge (planar) rather than raw 3D distance so the
    ///     stop range scales with mob size instead of one flat number that over/undershoots.
    /// </summary>
    private static bool WaitUntilNearARank()
    {
        if(!IsScreenReady() || !Player.Interactable) return false;

        var nearest = NearestARank();

        if(nearest == null) return false;

        var planarDistance = Vector2.Distance(new(Player.Position.X, Player.Position.Z), new(nearest.Position.X, nearest.Position.Z)) - nearest.HitboxRadius;

        if(planarDistance <= P.Config.StopNearARankDistance)
        {
            S.VNavmeshIPC.Stop();
            _lastCommandedTarget = null;
            return true;
        }

        // Only re-path once the mob has drifted meaningfully from the last commanded target, and
        // never while vnavmesh is still computing the previous route -- both a fixed-timer reissue
        // and an overlapping call cause the same jerky backtrack, just via different triggers.
        var driftedEnough = _lastCommandedTarget is not { } last || Vector3.Distance(last, nearest.Position) > 3f;
        if(driftedEnough && EzThrottler.Throttle("StopNearARankChaseLiveTarget", 1500) && S.VNavmeshIPC.TryMoveTo(nearest.Position, true))
        {
            _lastCommandedTarget = nearest.Position;
        }

        return false;
    }

    /// <summary>
    ///     The external auto-battle system only engages once we actually have a target -- stopping and
    ///     dismounting near the A-rank isn't enough on its own.
    /// </summary>
    private static bool TargetNearestARank()
    {
        if(!IsScreenReady() || !Player.Interactable) return false;

        var nearest = NearestARank();
        if(nearest == null) return false;

        if(Svc.Targets.Target?.GameObjectId == nearest.GameObjectId) return true;

        if(EzThrottler.Throttle("StopNearARankSetTarget", 500))
            Svc.Targets.Target = nearest;

        return false;
    }

    private static bool StopAndDismount() => Utils.TryDismount("StopNearARankDismount");

    /// <summary>
    ///     The stop distance is a flat radius around the mob, so it can easily land us behind terrain
    ///     with no line of sight -- e.g. the far side of a hill or rock the mob spawned next to. If so,
    ///     walk to the nearest of a ring of points around the mob that does have line of sight instead
    ///     of sitting there unable to engage.
    /// </summary>
    private static bool EnsureLineOfSight()
    {
        if(!IsScreenReady() || !Player.Interactable) return false;

        var target = NearestARank();
        if(target == null) return true;

        if(HasLineOfSight(target.Position))
        {
            S.VNavmeshIPC.Stop();
            _losMoveTarget = null;
            return true;
        }

        if(_losMoveTarget is { } dest)
        {
            var planarDistance = Vector2.Distance(new(Player.Position.X, Player.Position.Z), new(dest.X, dest.Z));
            if(planarDistance > 2f) return false; // still walking there
            _losMoveTarget = null; // arrived but somehow still no LoS -- fall through and try another spot
        }

        // Ran out of ring positions to try -- give up rather than blocking the task queue forever.
        if(_losSearchAttempt >= LosSearchPositions) return true;

        var angle = _losSearchAttempt * 2 * MathF.PI / LosSearchPositions;
        _losSearchAttempt++;
        var probeXZ = target.Position + new Vector3(LosSearchRadius * MathF.Cos(angle), 0, LosSearchRadius * MathF.Sin(angle));
        var probe = S.VNavmeshIPC.PointOnFloor(probeXZ with { Y = 1024f }, false, 3f);
        if(probe is { } point && HasLineOfSight(target.Position, point) &&
           EzThrottler.Throttle("StopNearARankLosMove", 500) && S.VNavmeshIPC.TryMoveTo(point, false))
        {
            _losMoveTarget = point;
        }

        return false;
    }

    private static IBattleNpc NearestARank() =>
        Svc.Objects
            .OfType<IBattleNpc>()
            .Where(x => x.IsTargetable && Utils.IsNpcIdInARankList(x.BaseId))
            .Where(x => _flagPos is not { } flag || Vector3.Distance(x.Position, flag) <= FlagSearchRadius)
            .OrderBy(x => Vector3.Distance(Player.Position, x.Position))
            .FirstOrDefault();

    private static bool HasLineOfSight(Vector3 targetPos, Vector3? fromPos = null)
    {
        var origin = (fromPos ?? Player.Position) + new Vector3(0, 2, 0);
        var destination = targetPos + new Vector3(0, 2, 0);
        var direction = destination - origin;
        var distance = direction.Length();
        if(distance <= 0.01f) return true;
        return !BGCollisionModule.RaycastMaterialFilter(origin, direction / distance, out _, distance);
    }
}
