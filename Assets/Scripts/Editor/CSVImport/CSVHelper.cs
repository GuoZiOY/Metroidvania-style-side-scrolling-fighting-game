using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class CSVHelper
{
    /// <summary>
    /// 解析 CSV 文本，返回 行→(列名→值) 的字典列表
    /// </summary>
    public static List<Dictionary<string, string>> Parse(string csvText)
    {
        var lines = new List<string>();
        bool inQuotes = false;
        StringBuilder sb = new StringBuilder();

        // 逐字符解析，正确处理引号内换行和逗号
        for (int i = 0; i < csvText.Length; i++)
        {
            char c = csvText[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < csvText.Length && csvText[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == '\n' && !inQuotes)
            {
                if (sb.Length > 0)
                {
                    lines.Add(sb.ToString().Trim('\r'));
                    sb.Clear();
                }
            }
            else if (c == '\r' && !inQuotes)
            {
                // 忽略 \r
            }
            else
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0)
            lines.Add(sb.ToString().Trim('\r'));

        if (lines.Count < 2)
        {
            Debug.LogWarning($"CSV 行数不足：{lines.Count}（需要至少表头+1行数据）");
            return new List<Dictionary<string, string>>();
        }

        // 解析表头
        string[] headers = ParseLine(lines[0]);
        var result = new List<Dictionary<string, string>>();

        for (int r = 1; r < lines.Count; r++)
        {
            string[] values = ParseLine(lines[r]);
            if (values.Length == 0 || (values.Length == 1 && string.IsNullOrWhiteSpace(values[0])))
                continue; // 跳过空行

            var row = new Dictionary<string, string>();
            for (int c = 0; c < headers.Length && c < values.Length; c++)
            {
                row[headers[c].Trim()] = values[c].Trim();
            }
            result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// 读文件并解析
    /// </summary>
    public static List<Dictionary<string, string>> ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"CSV 文件不存在: {filePath}");
            return null;
        }
        return Parse(File.ReadAllText(filePath, Encoding.UTF8));
    }

    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    // --- 类型安全的取值辅助 ---

    /// <summary>去除Excel公式包装：=001234 → 001234</summary>
    public static string CleanId(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        raw = raw.Trim();
        if (raw.StartsWith("=\"") && raw.EndsWith("\""))
            return raw.Substring(2, raw.Length - 3);
        if (raw.StartsWith("="))
            return raw.TrimStart('=').Trim('"');
        return raw;
    }

    public static string GetString(Dictionary<string, string> row, string key, string defaultValue = "")
    {
        return row.TryGetValue(key, out string val) && !string.IsNullOrWhiteSpace(val) ? val : defaultValue;
    }

    /// <summary>读取ID（自动去除Excel公式包装）</summary>
    public static string GetId(Dictionary<string, string> row, string key)
    {
        return CleanId(GetString(row, key));
    }

    public static int GetInt(Dictionary<string, string> row, string key, int defaultValue = 0)
    {
        if (row.TryGetValue(key, out string val) && int.TryParse(val, out int result))
            return result;
        return defaultValue;
    }

    public static float GetFloat(Dictionary<string, string> row, string key, float defaultValue = 0)
    {
        if (row.TryGetValue(key, out string val) && float.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result))
            return result;
        return defaultValue;
    }

    public static bool GetBool(Dictionary<string, string> row, string key, bool defaultValue = false)
    {
        if (row.TryGetValue(key, out string val))
        {
            val = val.Trim().ToLower();
            if (val == "true" || val == "1" || val == "yes") return true;
            if (val == "false" || val == "0" || val == "no") return false;
        }
        return defaultValue;
    }

    public static T GetEnum<T>(Dictionary<string, string> row, string key, T defaultValue = default) where T : struct, Enum
    {
        if (row.TryGetValue(key, out string val) && !string.IsNullOrWhiteSpace(val))
        {
            // 去掉空格、尝试精确匹配
            val = val.Trim();
            if (Enum.TryParse<T>(val, true, out T result))
                return result;
            // 尝试数字索引
            if (int.TryParse(val, out int idx) && Enum.IsDefined(typeof(T), idx))
                return (T)(object)idx;
        }
        return defaultValue;
    }
}
