using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R738 (user 11-21: "설정창, 플레이, 기물도감, 전적 UI 사운드도 넣어야 해"): the lobby's menu sounds (MenuSound). Every button
    // sounds as the pointer comes onto it and when clicked; which click sound by what it does:
    //   Tab      the top bar's 플레이 · 기물 도감 · 전적 · 순위, the codex's "전적 보기"
    //   Confirm  랜덤 매칭 / 모드 선택, 방 생성, 경기 시작, joining a party or a room, back into a left match, an invite taken
    //   Open     a window opening from the top bar (파티 · 친구 · 정보) or the bottom (코드로 참가, 모드)
    //   Close    a window's ✕, 매칭 취소, 파티 나가기, an invite turned down
    //   Piece    a piece in the codex's list (a chess piece set down)
    //   Toggle   백 / 흑, the bots' − +, the friend list's 온라인 / 전체, the ranking's 전체 / 내 주변 / 친구
    //   Page     the record's filters and ‹ ›, the friend list's pages, the ranking's boards and its ‹ › (R745)
    //   Click    everything else (a mode card, a friend's 초대, the name, 복사 …)
    // Esc closing a window or leaving a tab, Enter starting, and the mode cards rising under the pointer play their own.
    public sealed partial class NetworkHudView
    {
        static MenuSound.Kind? LobbySound(Button b)
        {
            string n = b.name ?? "";
            switch (n)
            {
                case "tab-play": case "tab-codex": case "tab-record": case "tab-rank": return MenuSound.Kind.Tab;
                case "play": case "create-test": case "start": case "join-party": case "join-test": case "rejoin": return MenuSound.Kind.Confirm;
                case "party-open": case "friends-open": case "details-open": case "code-open": case "mode-open": return MenuSound.Kind.Open;
                case "party-close": case "details-close": case "code-close": case "mode-close": case "friends-close":
                case "cancel": case "leave-party": return MenuSound.Kind.Close;
                case "bots-more": case "bots-less": case "ally-more": case "ally-less": case "enemy-more": case "enemy-less":
                case "friends-online": case "friends-all": return MenuSound.Kind.Toggle;
                case "friends-prev": case "friends-next": return MenuSound.Kind.Page;
            }
            if (b.ClassListContains("cx-item")) return MenuSound.Kind.Piece;
            if (b.ClassListContains("cx-seg")) return MenuSound.Kind.Toggle;
            if (b.ClassListContains("cx-record")) return MenuSound.Kind.Tab;
            if (b.ClassListContains("rc-filter") || b.ClassListContains("rc-page-btn")) return MenuSound.Kind.Page;
            // R743: the 순위 tab's boards (전체 · 폰 러쉬 · 소드 파이트, and the left's rows) and 전체 / 내 주변 / 친구; its ‹ › are
            // the record's rc-page-btn above.
            if (b.ClassListContains("rk-tab") || b.ClassListContains("rk-mode-row")) return MenuSound.Kind.Page;
            if (b.ClassListContains("rk-scope")) return MenuSound.Kind.Toggle;
            if (b.ClassListContains("invite-accept")) return MenuSound.Kind.Confirm;
            if (b.ClassListContains("invite-decline")) return MenuSound.Kind.Close;
            return null;
        }

        void WireSounds() => MenuSound.Wire(root, LobbySound);

        // A click the fallback router fired (no real pointer events reach the panel): the same sound.
        static void FallbackSound(Button button) => MenuSound.Play(MenuSound.KindOf(button, LobbySound));
    }
}
