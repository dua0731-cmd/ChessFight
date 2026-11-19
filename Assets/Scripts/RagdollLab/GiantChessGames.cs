using System;
using UnityEngine;
namespace ChessFight.RagdollLab
{
    [Serializable] public sealed class GiantChessGame
    {
        public string id, white, black, date, round, result;
        public string[] rows;
        public string Title => id+" · "+white+" — "+black+" · "+date;
    }
    [Serializable] public sealed class GiantChessGames
    {
        public GiantChessGame[] games;
        static int previous = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSelection() => previous = -1;
        public GiantChessGame Pick()
        {
            if(games==null || games.Length==0) throw new InvalidOperationException("GM replay library is empty.");
            int n=games.Length;
            int index=UnityEngine.Random.Range(0,previous>=0 && previous<n && n>1?n-1:n);
            if(n>1 && previous>=0 && previous<n && index>=previous)index++;
            previous=index;return games[index];
        }
    }
}
