using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class FortuneCountData
{
    public List<DailyFortuneCount> dailyCounts = new();
}

[Serializable]
public sealed class DailyFortuneCount
{
    public string date;
    public int count;
}

public static class FortuneCountStorage
{
    private const string FileName = "fortune_counts.json";
    private const string DateFormat = "yyyy-MM-dd";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool IncreaseTodayCount()
    {
        FortuneCountData data = Load();
        if (data == null)
        {
            return false;
        }

        string today = DateTime.Now.ToString(DateFormat);
        DailyFortuneCount todayEntry = data.dailyCounts.Find(entry => entry.date == today);

        if (todayEntry == null)
        {
            todayEntry = new DailyFortuneCount
            {
                date = today,
                count = 0
            };
            data.dailyCounts.Add(todayEntry);
        }

        todayEntry.count++;
        return Save(data);
    }

    public static int GetDailyCount(DateTime date)
    {
        FortuneCountData data = Load();
        if (data == null)
        {
            return 0;
        }

        string dateKey = date.ToString(DateFormat);
        DailyFortuneCount entry = data.dailyCounts.Find(item => item.date == dateKey);
        return entry != null ? entry.count : 0;
    }

    public static int GetMonthlyCount(int year, int month)
    {
        if (month < 1 || month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        FortuneCountData data = Load();
        if (data == null)
        {
            return 0;
        }

        string monthPrefix = $"{year:D4}-{month:D2}-";
        int total = 0;

        foreach (DailyFortuneCount entry in data.dailyCounts)
        {
            if (entry.date != null && entry.date.StartsWith(monthPrefix, StringComparison.Ordinal))
            {
                total += entry.count;
            }
        }

        return total;
    }

    public static int GetTotalCount()
    {
        FortuneCountData data = Load();
        if (data == null)
        {
            return 0;
        }

        int total = 0;
        foreach (DailyFortuneCount entry in data.dailyCounts)
        {
            total += entry.count;
        }

        return total;
    }

    private static FortuneCountData Load()
    {
        if (!File.Exists(FilePath))
        {
            return new FortuneCountData();
        }

        try
        {
            string json = File.ReadAllText(FilePath);
            FortuneCountData data = JsonUtility.FromJson<FortuneCountData>(json);
            if (data == null)
            {
                Debug.LogError($"[FortuneCountStorage] Count data is invalid: {FilePath}");
                return null;
            }

            data.dailyCounts ??= new List<DailyFortuneCount>();
            return data;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[FortuneCountStorage] Failed to load count data: {exception.Message}");
            return null;
        }
    }

    private static bool Save(FortuneCountData data)
    {
        try
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[FortuneCountStorage] Failed to save count data: {exception.Message}");
            return false;
        }
    }
}
