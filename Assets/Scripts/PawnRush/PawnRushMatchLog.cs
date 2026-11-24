using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R742 (user 11-21: the 전적 tab's detail of a match, mock-up A): what this player did in a Pawn Rush match, written
    // into the match record's log (MatchRecords.Log; nothing while no online match is armed, so offline and spectators
    // write nothing). On CrownFinish's object, on every PC; nothing is sent.
    //   sections     the one this player's pawn stands in (PawnRushRound.SectionAt; the corridors between keep the last)
    //   falls        the course rules every PC hears for its own pawn (as PawnRushFallNotice does): a kill volume, the
    //                water, a fall past the drop rule, the other team's zone - one fall however many of them fire
    //   checkpoints  each new one reached (Checkpoint.Reached, numbered on through the round)
    //   promotions   the pawn's piece changing
    // Times are the race clock (CrownFinish.Clock); a client's log moves onto the host's clock when it hears it.
    // Skills, dives and grabs are counted by the network link (SteamPawnRushLink), which knows the skill moments.
    public sealed class PawnRushMatchLog : MonoBehaviour
    {
        // Rules that fire this close together are one fall (a pawn crosses a kill volume into the water).
        const float SameFall = 1.5f;

        CrownFinish finish;
        float lastFall = -10f;
        int lastCheckpoint = int.MinValue;
        PieceKind? lastPiece;

        void Awake() => finish = GetComponent<CrownFinish>();

        void OnEnable()
        {
            KillVolume.Entered += OnKill;
            WaterZone.Entered += OnWater;
            FallDistanceRespawn.Fell += OnFell;
            TeamZone.Intruded += OnIntruded;
            Checkpoint.Reached += OnCheckpoint;
        }

        void OnDisable()
        {
            KillVolume.Entered -= OnKill;
            WaterZone.Entered -= OnWater;
            FallDistanceRespawn.Fell -= OnFell;
            TeamZone.Intruded -= OnIntruded;
            Checkpoint.Reached -= OnCheckpoint;
        }

        CrownFinish.Runner Me => finish != null ? finish.Self : null;

        bool Mine(ICharacterDriver who)
        {
            var me = Me;
            return MatchRecords.Log != null && me != null && who != null && who == me.Driver && finish.Winner == PawnRushFinish.None;
        }

        void Fall(ICharacterDriver who)
        {
            if (!Mine(who) || Time.time - lastFall < SameFall) return;
            lastFall = Time.time;
            MatchRecords.Log.Fall();
        }

        void OnKill(ICharacterDriver who, KillVolume _) => Fall(who);
        void OnWater(ICharacterDriver who, WaterZone _) => Fall(who);
        void OnFell(ICharacterDriver who, FallDistanceRespawn _) => Fall(who);
        void OnIntruded(ICharacterDriver who, TeamZone _) => Fall(who);

        void OnCheckpoint(ICharacterDriver who, Checkpoint checkpoint)
        {
            if (!Mine(who) || checkpoint == null || checkpoint.Order <= lastCheckpoint) return;
            lastCheckpoint = checkpoint.Order;
            MatchRecords.Log.Checkpoints++;
        }

        void Update()
        {
            var log = MatchRecords.Log;
            var me = Me;
            if (log == null || me == null || me.Driver == null || finish.Winner != PawnRushFinish.None) return;
            float clock = finish.Clock;
            var hips = me.Driver.FollowTarget;
            var round = PawnRushRound.Current;
            if (hips != null && round != null)
            {
                var section = round.SectionAt(hips.position, out _);
                if (section != null) log.Enter(section.DisplayName, clock);
            }
            if (me.Driver is IPromotable promotable)
            {
                var piece = promotable.Piece;
                if (lastPiece.HasValue && lastPiece.Value != piece) log.Promote(piece, clock);
                lastPiece = piece;
            }
        }
    }
}
