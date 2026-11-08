using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // What a Pawn Rush match runs on, for the online link (R508): the stations in the order the course state names them,
    // the crown finish, and the course's own part of the state. Two kinds:
    //   course 01 (the prototype)   two plazas, four stations; the host draws the two games and sends them, and the
    //                               promotion pads (pieces, spent, who is choosing)
    //   a section round (the match) each mini-game section's white then black station (six for three sections); the games
    //                               come with the sections every PC drew from the match's number, there are no pads, and
    //                               the sections' own state (ISectionNetPart) rides along as one run of bytes
    public abstract class MatchCourse
    {
        public abstract int StationCount { get; }
        public abstract MissionStation Station(int index);
        public abstract CrownFinish Finish { get; }

        // Host: the course's own fields of the state (the link writes the stations and the race).
        public abstract void Write(PawnRushNetState state);
        // Not the host, before the stations are read (course 01 installs the host's draw) and after.
        public virtual void ApplyBefore(PawnRushNetState state) { }
        public abstract void ApplyAfter(PawnRushNetState state);
        // The match's shared start is known (the lobby's go, on the obstacle clock).
        public virtual void StartAt(double clock) { }

        public static MatchCourse Of(PawnRushCourse course) => course != null ? new Course01Match(course) : null;
        public static MatchCourse Of(PawnRushRound round) => round != null ? new RoundMatch(round) : null;
    }

    sealed class Course01Match : MatchCourse
    {
        readonly PawnRushCourse course;

        public Course01Match(PawnRushCourse course) { this.course = course; }

        MissionPicker Picker => course != null ? course.Missions : null;

        public override int StationCount => Picker != null ? 4 : 0;
        // Plaza 1 white, plaza 1 black, plaza 2 white, plaza 2 black.
        public override MissionStation Station(int index) => Picker != null ? Picker.Station(index / 2 + 1, index % 2) : null;
        public override CrownFinish Finish => course != null ? course.Finish : null;

        public override void Write(PawnRushNetState state)
        {
            var picker = Picker;
            if (picker == null) return;
            state.First = (sbyte)picker.First;
            state.Second = (sbyte)picker.Second;
            state.PadFirst = (byte)picker.PadFirst;
            state.PadSecond = (byte)picker.PadSecond;
            state.PadsUsed = picker.PadsUsed;
            picker.Choosers(state.PadChooser);
        }

        public override void ApplyBefore(PawnRushNetState state)
        {
            var picker = Picker;
            if (picker != null && state.First >= 0 && state.Second >= 0 && (picker.First != state.First || picker.Second != state.Second))
                picker.Install(state.First, state.Second);
        }

        public override void ApplyAfter(PawnRushNetState state)
        {
            var picker = Picker;
            if (picker != null) picker.ApplyPads((PieceKind)state.PadFirst, (PieceKind)state.PadSecond, state.PadsUsed, state.PadChooser);
        }
    }

    sealed class RoundMatch : MatchCourse
    {
        readonly PawnRushRound round;
        readonly List<MissionStation> stations = new List<MissionStation>();
        readonly List<ISectionNetPart> parts = new List<ISectionNetPart>();
        int listedSeed, listedSections = -1;
        bool overflowTold;

        public RoundMatch(PawnRushRound round) { this.round = round; }

        // The round's stations and parts, listed again whenever it is another round (offline only: a match builds once).
        void List()
        {
            if (round == null || (listedSections == round.Sections.Count && listedSeed == round.Seed)) return;
            listedSeed = round.Seed;
            listedSections = round.Sections.Count;
            stations.Clear();
            parts.Clear();
            foreach (var section in round.Sections)
            {
                if (section == null) continue;
                var games = section.GetComponent<MiniGameSection>();
                if (games != null && games.Stations != null)
                    for (int team = 0; team < 2; team++)
                        foreach (var station in games.Stations)
                            if (station != null && station.Team == team && stations.Count < PawnRushNetState.MaxStations) stations.Add(station);
                parts.AddRange(section.GetComponentsInChildren<ISectionNetPart>(true));
            }
        }

        public override int StationCount { get { List(); return stations.Count; } }

        public override MissionStation Station(int index)
        {
            List();
            return index >= 0 && index < stations.Count ? stations[index] : null;
        }

        public override CrownFinish Finish => round != null ? round.Finish : null;

        public override void Write(PawnRushNetState state)
        {
            List();
            var block = new NetBlock(state.Parts, 0, PawnRushNetState.MaxPartBytes);
            foreach (var part in parts)
            {
                if (!Fits(block, part.NetBytes)) break;
                int start = block.At;
                part.WriteNet(ref block);
                block.At = start + part.NetBytes;   // exactly its bytes, whatever it wrote
            }
            state.PartsLength = block.Written;
        }

        public override void ApplyAfter(PawnRushNetState state)
        {
            List();
            var block = new NetBlock(state.Parts, 0, Mathf.Min(state.PartsLength, PawnRushNetState.MaxPartBytes));
            foreach (var part in parts)
            {
                if (!block.Left(part.NetBytes)) break;
                int start = block.At;
                part.ReadNet(ref block);
                block.At = start + part.NetBytes;
            }
        }

        public override void StartAt(double clock)
        {
            if (round != null) round.SetStartClock(clock);
        }

        bool Fits(NetBlock block, int bytes)
        {
            if (block.Left(bytes)) return true;
            if (!overflowTold)
            {
                overflowTold = true;
                Debug.LogWarning("[PawnRush] 이번 판 구간들의 온라인 상태가 " + PawnRushNetState.MaxPartBytes + "바이트를 넘어 뒤쪽 일부는 보내지 않습니다.");
            }
            return false;
        }
    }
}
