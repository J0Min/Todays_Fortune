using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves the selected rope/card pair against the fortune TSV and saves its final ID.
/// </summary>
public sealed class FortuneDataReader_Save : MonoBehaviour
{
    [SerializeField] private TextAsset fortuneTsv;

    private readonly Dictionary<(int ropeId, int cardId), int> finalIdBySelection = new();
    private bool isLoaded;

    private void Start()
    {
        SaveFinalIdFromSelection();
    }

    public bool SaveFinalIdFromSelection()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError("[FortuneDataReader_Save] PlayerFortuneState.Instance is missing.", this);
            return false;
        }

        if (!state.IsValidRopeId(state.RopeId) || !state.IsValidCardId(state.CardId))
        {
            Debug.LogError(
                $"[FortuneDataReader_Save] Invalid selection: RopeId={state.RopeId}, CardId={state.CardId}.",
                this);
            return false;
        }

        if (!TryGetFinalId(state.RopeId, state.CardId, out int finalId))
        {
            return false;
        }

        state.SetID(finalId);
        return true;
    }

    public bool TryGetFinalId(int ropeId, int cardId, out int finalId)
    {
        finalId = 0;
        if (!LoadData())
        {
            return false;
        }

        if (finalIdBySelection.TryGetValue((ropeId, cardId), out finalId))
        {
            return true;
        }

        Debug.LogError(
            $"[FortuneDataReader_Save] No TSV row for RopeId={ropeId}, CardId={cardId}.",
            this);
        return false;
    }

    private bool LoadData()
    {
        if (isLoaded)
        {
            return true;
        }

        if (fortuneTsv == null)
        {
            Debug.LogError("[FortuneDataReader_Save] Fortune TSV is not assigned.", this);
            return false;
        }

        finalIdBySelection.Clear();
        string[] lines = fortuneTsv.text.Split('\n');
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] columns = line.Split('\t');
            if (columns.Length < 4 ||
                !int.TryParse(columns[0], out int finalId) ||
                !int.TryParse(columns[1], out int ropeId) ||
                !int.TryParse(columns[3], out int cardId))
            {
                Debug.LogError(
                    $"[FortuneDataReader_Save] Invalid TSV row {lineIndex + 1}. " +
                    "Expected id, rope_id, rope, card_id columns.",
                    this);
                continue;
            }

            if (!finalIdBySelection.TryAdd((ropeId, cardId), finalId))
            {
                Debug.LogError(
                    $"[FortuneDataReader_Save] Duplicate selection in TSV: RopeId={ropeId}, CardId={cardId}.",
                    this);
            }
        }

        if (finalIdBySelection.Count == 0)
        {
            Debug.LogError("[FortuneDataReader_Save] No valid TSV data was loaded.", this);
            return false;
        }

        isLoaded = true;
        return true;
    }
}
