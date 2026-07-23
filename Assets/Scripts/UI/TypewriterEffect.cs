using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 文本打字机工具。静态调用，无需挂载。
/// </summary>
public static class TypewriterEffect
{
    private static MonoBehaviour _runner;

    /// <summary>初始化运行器（首次调用自动创建）</summary>
    private static void EnsureRunner()
    {
        if (_runner == null)
        {
            var go = new GameObject("[TypewriterRunner]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<TypewriterRunner>();
        }
    }

    /// <summary>逐字打出文本</summary>
    /// <param name="text">目标 TMP 组件</param>
    /// <param name="content">完整文本</param>
    /// <param name="charInterval">每个字间隔（秒）</param>
    /// <param name="punctuationDelay">标点额外停顿（秒）</param>
    /// <param name="onComplete">完成回调</param>
    public static void Play(TMP_Text text, string content,
        float charInterval = 0.15f,
        float punctuationDelay = 0.3f,
        System.Action onComplete = null)
    {
        if (text == null) return;
        EnsureRunner();
        _runner.StartCoroutine(TypeText(text, content, charInterval, punctuationDelay, onComplete));
    }

    /// <summary>立即显示完整文本（停止当前打字）</summary>
    public static void Skip(TMP_Text text)
    {
        if (text == null) return;
        text.maxVisibleCharacters = text.text.Length;
    }

    private static IEnumerator TypeText(TMP_Text text, string content,
        float charInterval, float punctuationDelay, System.Action onComplete)
    {
        text.text = content;
        text.ForceMeshUpdate();
        int visibleCount = text.textInfo.characterCount;  // 忽略富文本标签的实际可见字数
        text.maxVisibleCharacters = 0;

        AudioManager.Instance?.PlayTypewriterSfx();

        for (int i = 0; i < visibleCount; i++)
        {
            text.maxVisibleCharacters = i + 1;

            // 获取当前可见字符（用于检测标点）
            var charInfo = text.textInfo.characterInfo[i];
            if (charInfo.character == '.' || charInfo.character == '。' ||
                charInfo.character == '！' || charInfo.character == '？' ||
                charInfo.character == ',' || charInfo.character == '，' ||
                charInfo.character == ' ')
                yield return new WaitForSecondsRealtime(punctuationDelay);
            else
                yield return new WaitForSecondsRealtime(charInterval);
        }

        AudioManager.Instance?.StopTypewriterSfx();
        onComplete?.Invoke();
    }

    /// <summary>内部运行器（隐藏 GameObject）</summary>
    private class TypewriterRunner : MonoBehaviour { }
}
