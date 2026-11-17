using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R694 (the user 2026-11-17, "방 생성"): the entrance screen as the waiting room of a room a player made
    // (State.Room, while State.Matching). The two sides' lists meet in the middle with "VS" between them, the
    // spectators' seats stand in a row below ("관전석 UI만 하단으로 옮겨주고 가운데 빈 공간을 양쪽 팀 UI를 좁혀서
    // 메워줘"), and the room's number with "복사" under the title.
    //
    //   - An empty seat on a side or among the spectators is a button: a click moves this PC's player there
    //     (SeatRequested: 0 / 1 a side, 2 a spectator's seat) when State.CanSit says it may.
    //   - On the manager's screen a player's card has "옮기기" beside "양도" and "강퇴" (MoveRequested with the
    //     player's key; the session picks where to: the other side, or a spectator's seat when that side is full), and
    //     each side's head "봇 −" / "봇 +" (TeamBotRequested).
    //   - "매칭 취소" reads "방 나가기"; a line over the buttons (State.RoomNote) says how many sit where and what the
    //     start waits for.
    //
    // Everything here is built in code and shown by the class "load-room" on the screen's root (NetworkHud.uss, R694);
    // once the room starts the class goes and the same screen carries on as the loading screen.
    public sealed partial class LoadingScreenView
    {
        public event Action<int> SeatRequested;
        public event Action<ulong> MoveRequested;
        public event Action CopyRoomRequested;
        public event Action<int, int> TeamBotRequested;

        sealed class SpecCard
        {
            public VisualElement Root;
            public Label Name;
            public Button Sit, Move, Kick;
            public GlassPlate Plate;
            public ulong Key;
            public float KickArmedAt = -10;
        }

        VisualElement roomHead, specBox;
        Label roomCode, roomNote, specCount;
        readonly List<SpecCard> specCards = new List<SpecCard>();
        readonly Button[] botMore = new Button[2], botLess = new Button[2];

        void BuildRoom()
        {
            if (screen == null) return;
            roomHead = Box("load-room-head");
            roomHead.Add(Text("방 번호", "load-room-label"));
            roomCode = Text("", "load-room-code");
            roomHead.Add(roomCode);
            var copy = Small("복사", "load-room-copy");
            copy.clicked += () => CopyRoomRequested?.Invoke();
            roomHead.Add(copy);
            screen.Add(roomHead);

            var versus = Text("VS", "load-room-vs");
            versus.AddToClassList("disp");
            RuntimePanels.Display(versus);
            screen.Add(versus);

            specBox = Box("load-spec");
            var head = Box("load-spec-head");
            specCount = Text("", "load-spec-count");
            head.Add(specCount);
            var gap = new VisualElement { pickingMode = PickingMode.Ignore };
            gap.AddToClassList("grow");
            head.Add(gap);
            head.Add(Text("팀이 꽉 찼으면 여기로 잠시 옮겼다가 조율해요", "load-spec-hint"));
            specBox.Add(head);
            var row = Box("load-spec-row");
            for (int i = 0; i < SpectatorCards; i++) row.Add(NewSpecCard().Root);
            specBox.Add(row);
            screen.Add(specBox);

            roomNote = Text("", "load-room-note");
            screen.Add(roomNote);

            for (int team = 0; team < 2; team++)
            {
                var rosterHead = rosters[team]?.Q<VisualElement>(className: "load-roster-head");
                if (rosterHead == null) continue;
                int side = team;
                botLess[team] = Small("봇 −", "load-team-bot");
                botLess[team].clicked += () => TeamBotRequested?.Invoke(side, -1);
                botMore[team] = Small("봇 +", "load-team-bot");
                botMore[team].clicked += () => TeamBotRequested?.Invoke(side, 1);
                // White's head reads team, (room), count; black's count, (room), team: the buttons go next to the team's name.
                int at = team == 0 ? 1 : rosterHead.childCount - 1;
                rosterHead.Insert(at, botMore[team]);
                rosterHead.Insert(at, botLess[team]);
            }
        }

        // As many as the room has (SteamSession.SpectatorSeats; the Game assembly does not see the session).
        const int SpectatorCards = 4;

        SpecCard NewSpecCard()
        {
            var card = new SpecCard { Root = Box("load-spec-card") };
            card.Plate = GlassPlate.On(card.Root, 10f);
            card.Plate.Set(false, true);
            card.Name = Text("빈 관전석", "load-spec-name");
            card.Root.Add(card.Name);
            var gap = new VisualElement { pickingMode = PickingMode.Ignore };
            gap.AddToClassList("grow");
            card.Root.Add(gap);
            card.Move = Small("옮기기", "load-card-btn");
            card.Move.clicked += () => { if (card.Key != 0) MoveRequested?.Invoke(card.Key); };
            card.Root.Add(card.Move);
            card.Kick = Small("강퇴", "load-card-btn");
            card.Kick.AddToClassList("a-danger");
            card.Kick.clicked += () =>
            {
                if (card.Key == 0) return;
                if (Time.unscaledTime - card.KickArmedAt < ArmSeconds) { card.KickArmedAt = -10; KickRequested?.Invoke(card.Key); }
                else card.KickArmedAt = Time.unscaledTime;
            };
            card.Root.Add(card.Kick);
            card.Sit = SeatButton(SpectatorSeat);
            card.Root.Add(card.Sit);
            specCards.Add(card);
            return card;
        }

        const int SpectatorSeat = 2;

        // The whole of an empty seat as a button.
        Button SeatButton(int seat)
        {
            var button = new Button { text = "", focusable = false };
            button.AddToClassList("load-seat-btn");
            button.style.display = DisplayStyle.None;
            button.clicked += () => SeatRequested?.Invoke(seat);
            return button;
        }

        static VisualElement Box(string name)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList(name);
            return box;
        }

        static Label Text(string text, string name)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(name);
            return label;
        }

        static Button Small(string text, string name)
        {
            var button = new Button { text = text, focusable = false };
            button.AddToClassList("a-btn");
            button.AddToClassList("a-sm");
            button.AddToClassList(name);
            button.style.display = DisplayStyle.None;
            return button;
        }

        static void Shown(VisualElement element, bool on)
        {
            if (element != null) element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SyncRoom(float now)
        {
            if (screen == null || roomHead == null) return;
            bool room = State.Room && State.Matching && !Hiding;
            screen.EnableInClassList("load-room", room);
            if (cancel != null)
            {
                string leave = State.Room && State.Matching ? "방 나가기" : "매칭 취소";
                if (cancel.text != leave) cancel.text = leave;
            }
            Shown(roomHead.Q<Button>(className: "load-room-copy"), room);
            for (int team = 0; team < 2; team++)
            {
                Shown(botMore[team], room && State.ShowTeamBots);
                Shown(botLess[team], room && State.ShowTeamBots);
                botMore[team]?.SetEnabled(State.CanAddTeamBot[team]);
                botLess[team]?.SetEnabled(State.CanRemoveTeamBot[team]);
            }
            if (!room) return;

            if (roomCode.text != State.RoomCode) roomCode.text = State.RoomCode;
            if (roomNote.text != State.RoomNote) roomNote.text = State.RoomNote;
            int seated = Mathf.Min(State.SpecNames.Count, specCards.Count);
            string count = "관전석 " + seated + " / " + specCards.Count;
            if (specCount.text != count) specCount.text = count;
            for (int i = 0; i < specCards.Count; i++)
            {
                var card = specCards[i];
                bool present = i < seated;
                ulong key = present && i < State.SpecKeys.Count ? State.SpecKeys[i] : 0;
                if (card.Key != key) card.KickArmedAt = -10;
                card.Key = key;
                string name = present ? State.SpecNames[i] : State.CanSit[SpectatorSeat] ? "빈 관전석 · 눌러서 이동" : "빈 관전석";
                if (card.Name.text != name) card.Name.text = name;
                bool me = present && i == State.SpecMe;
                card.Root.EnableInClassList("load-spec-on", present);
                card.Root.EnableInClassList("load-me", me);
                card.Plate.Set(me, !present);
                Shown(card.Sit, !present && State.CanSit[SpectatorSeat]);
                Shown(card.Move, present && i < State.SpecCanMove.Count && State.SpecCanMove[i]);
                bool kick = present && i < State.SpecCanKick.Count && State.SpecCanKick[i];
                Shown(card.Kick, kick);
                if (!kick) card.KickArmedAt = -10;
                Arm(card.Kick, kick && now - card.KickArmedAt < ArmSeconds, "강퇴", "load-card-kick-armed");
            }
        }

        // A side's seat in the room: an empty one is a button when this PC's player may sit there.
        void RoomSeat(Card card, bool empty)
        {
            bool room = State.Room && State.Matching && !Hiding;
            bool can = room && empty && State.CanSit[card.Team];
            Shown(card.Sit, can);
            if (room && empty) card.Status.text = can ? "눌러서 이동" : "";
        }
    }
}
