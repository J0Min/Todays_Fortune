using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class FortuneDataReader : MonoBehaviour
{
    [Serializable]
    public sealed class FortuneData
    {
        public int RopeId { get; }
        public string Rope { get; }
        public int CardId { get; }
        public string Card { get; }
        public string Title { get; }
        public string Description { get; }

        public FortuneData(
            int ropeId,
            string rope,
            int cardId,
            string card,
            string title,
            string description)
        {
            RopeId = ropeId;
            Rope = rope;
            CardId = cardId;
            Card = card;
            Title = title;
            Description = description;
        }
    }

    [SerializeField] private TextAsset fortuneTsv;

    private readonly Dictionary<(int ropeId, int cardId), FortuneData> fortuneByIds = new();

    private void Start()
    {
        if (!LoadFortuneData())
        {
            return;
        }

        if (TryGetFortune(1, 3, out FortuneData fortune))
        {
            Debug.Log(
                $"[Fortune] 동아줄: {fortune.Rope} / 카드: {fortune.Card}\n" +
                $"제목: {fortune.Title}\n설명: {fortune.Description}");
        }
    }

    public bool TryGetFortune(int ropeId, int cardId, out FortuneData fortune)
    {
        if (fortuneByIds.Count == 0 && !LoadFortuneData())
        {
            fortune = null;
            return false;
        }

        if (fortuneByIds.TryGetValue((ropeId, cardId), out fortune))
        {
            return true;
        }

        Debug.LogError($"rope_id={ropeId}, card_id={cardId}에 해당하는 운세 데이터를 찾지 못했습니다.");
        return false;
    }

    private bool LoadFortuneData()
    {
        if (fortuneTsv == null)
        {
            Debug.LogError("FortuneDataReader의 Fortune Tsv에 TSV 파일이 연결되지 않았습니다.", this);
            return false;
        }

        fortuneByIds.Clear();
        string[] lines = fortuneTsv.text.Split('\n');

        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] columns = line.Split('\t');
            if (columns.Length < 7)
            {
                Debug.LogError($"TSV {lineIndex + 1}행의 열 개수가 부족합니다. (필요: 7, 실제: {columns.Length})", this);
                continue;
            }

            if (!int.TryParse(columns[1], out int ropeId) ||
                !int.TryParse(columns[3], out int cardId))
            {
                Debug.LogError($"TSV {lineIndex + 1}행의 rope_id 또는 card_id가 올바른 정수가 아닙니다.", this);
                continue;
            }

            FortuneData fortune = new FortuneData(
                ropeId,
                columns[2],
                cardId,
                columns[4],
                columns[5],
                columns[6]);

            if (!fortuneByIds.TryAdd((ropeId, cardId), fortune))
            {
                Debug.LogError($"TSV {lineIndex + 1}행에 중복된 조합이 있습니다: rope_id={ropeId}, card_id={cardId}", this);
            }
        }

        if (fortuneByIds.Count == 0)
        {
            Debug.LogError("TSV에서 읽을 수 있는 운세 데이터가 없습니다.", this);
            return false;
        }

        return true;
    }
}

#if UNITY_EDITOR
[UnityEditor.AssetImporters.ScriptedImporter(1, "tsv")]
internal sealed class FortuneTsvImporter : UnityEditor.AssetImporters.ScriptedImporter
{
    public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext context)
    {
        string text = System.IO.File.ReadAllText(context.assetPath, System.Text.Encoding.UTF8);
        TextAsset textAsset = new TextAsset(text);
        context.AddObjectToAsset("Fortune TSV", textAsset);
        context.SetMainObject(textAsset);
    }
}
#endif
