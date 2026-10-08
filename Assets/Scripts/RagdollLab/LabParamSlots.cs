using System;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Four saved parameter sets so a playtest can switch between them instantly, optionally blind
    /// (the panel hides which set is live), with a good/bad tally per set.
    /// </summary>
    public class LabParamSlots : MonoBehaviour
    {
        public const int Count = 4;
        public static readonly string[] Names = { "A", "B", "C", "D" };

        public RagdollTuning tuning;
        public LabGame game;

        [Serializable]
        class SaveFile
        {
            public RagdollParams[] sets = new RagdollParams[Count];
            public bool[] filled = new bool[Count];
            public int[] good = new int[Count];
            public int[] bad = new int[Count];
        }

        readonly SaveFile data = new SaveFile();
        public int Active { get; private set; } = -1;
        public bool Blind { get; set; }

        public string Path => System.IO.Path.Combine(Application.persistentDataPath, "RagdollLabSlots.json");

        public bool Filled(int slot) => data.filled[slot];
        public int Good(int slot) => data.good[slot];
        public int Bad(int slot) => data.bad[slot];
        public int Votes => data.good[0] + data.bad[0] + data.good[1] + data.bad[1] + data.good[2] + data.bad[2] + data.good[3] + data.bad[3];

        void Awake()
        {
            for (int i = 0; i < Count; i++) data.sets[i] = new RagdollParams();
            Restore();
        }

        public void Save(int slot)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(tuning.values), data.sets[slot]);
            data.filled[slot] = true;
            Active = slot;
            Persist();
            game.Status = $"{Names[slot]}에 현재 값을 저장했어요";
        }

        public void Load(int slot)
        {
            if (!data.filled[slot])
            {
                game.Status = $"{Names[slot]}는 아직 비어 있어요 (Shift+{slot + 1}로 저장)";
                return;
            }
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data.sets[slot]), tuning.values);
            Active = slot;
            game.MarkTuningDirty();
            game.Status = Blind ? "값 세트를 바꿨어요 (어느 것인지는 숨김)" : $"{Names[slot]} 값 세트로 바꿨어요";
        }

        /// <summary>Blind test: jump to another saved set without saying which.</summary>
        public void Randomize()
        {
            int filled = 0;
            for (int i = 0; i < Count; i++) if (data.filled[i]) filled++;
            if (filled == 0)
            {
                game.Status = "저장된 값 세트가 없어요";
                return;
            }
            for (int guard = 0; guard < 32; guard++)
            {
                int pick = UnityEngine.Random.Range(0, Count);
                if (!data.filled[pick] || (filled > 1 && pick == Active)) continue;
                Load(pick);
                game.Status = "값 세트를 무작위로 바꿨어요";
                return;
            }
        }

        public void Vote(bool liked)
        {
            if (Active < 0)
            {
                game.Status = "먼저 값 세트를 불러와야 평가할 수 있어요";
                return;
            }
            if (liked) data.good[Active]++;
            else data.bad[Active]++;
            Persist();
            game.Status = liked ? "좋음으로 기록했어요" : "별로로 기록했어요";
        }

        public void ClearVotes()
        {
            for (int i = 0; i < Count; i++)
            {
                data.good[i] = 0;
                data.bad[i] = 0;
            }
            Persist();
            game.Status = "평가 기록을 지웠어요";
        }

        public string Summary(int slot)
        {
            if (!data.filled[slot]) return "비어 있음";
            return $"좋음 {data.good[slot]} / 별로 {data.bad[slot]}";
        }

        void Persist()
        {
            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                game.Status = "값 세트 저장 실패: " + e.Message;
            }
        }

        void Restore()
        {
            try
            {
                if (File.Exists(Path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(Path), data);
            }
            catch (Exception)
            {
                // A broken file just means we start from empty slots.
            }
        }
    }
}
