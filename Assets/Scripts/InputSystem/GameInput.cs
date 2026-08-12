using System.Collections.Generic;
using UnityEngine;

public static class GameInput
{
    public enum Action
    {
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,

        Jump,
        Dash,
        Attack,
        CounterAttack,
        PlatformDrop,

        SkillSlot1,
        SkillSlot2,
        SkillSlot3,
        SkillSlot4,
        SkillSlot5,
        DomainExpansion,

        ToggleCharacterPanel,
        ToggleSkillPanel,
        ToggleSettingsPanel,
        ToggleQuestPanel,

        Interact,
        Escape,
    }

    public static event System.Action OnBindingsChanged;

    private static Dictionary<Action, KeyCode> s_bindings;
    private const string PlayerPrefsKey = "GameInputBindings";

    static GameInput()
    {
        LoadBindings();
    }

    public static KeyCode GetBinding(Action action)
    {
        return s_bindings.TryGetValue(action, out KeyCode key) ? key : KeyCode.None;
    }

    public static void SetBinding(Action action, KeyCode key)
    {
        s_bindings[action] = key;
        SaveBindings();
        OnBindingsChanged?.Invoke();
    }

    public static void ResetToDefaults()
    {
        s_bindings = new Dictionary<Action, KeyCode>(Defaults);
        SaveBindings();
        OnBindingsChanged?.Invoke();
    }

    public static bool IsPlayerControlBlocked; // Boss 出场等锁定玩家操作（BossEncounter 置位）

    public static bool IsGameBlocked => Networking.UI_Chat.IsChatFocused || UIManager.IsAnyPanelOpen || IsPlayerControlBlocked;

    public static bool GetKeyDown(Action action)
    {
        if (IsGameBlocked && !IsToggleAction(action)) return false;
        return Input.GetKeyDown(GetBinding(action));
    }
    public static bool GetKey(Action action)
    {
        if (IsGameBlocked && !IsToggleAction(action)) return false;
        return Input.GetKey(GetBinding(action));
    }
    public static bool GetKeyUp(Action action)
    {
        if (IsGameBlocked && !IsToggleAction(action)) return false;
        return Input.GetKeyUp(GetBinding(action));
    }

    private static bool IsToggleAction(Action action) => action switch
    {
        Action.ToggleCharacterPanel or Action.ToggleSkillPanel or
        Action.ToggleSettingsPanel or Action.ToggleQuestPanel or
        Action.Interact or Action.Escape => true,
        _ => false,
    };

    // Digital axis — computed from held keys, no Unity Input Manager dependency
    public static float Horizontal
    {
        get
        {
            if (IsGameBlocked) return 0;
            float r = GetKey(Action.MoveRight) ? 1 : 0;
            float l = GetKey(Action.MoveLeft) ? 1 : 0;
            return r - l;
        }
    }
    public static float Vertical
    {
        get
        {
            if (IsGameBlocked) return 0;
            float u = GetKey(Action.MoveUp) ? 1 : 0;
            float d = GetKey(Action.MoveDown) ? 1 : 0;
            return u - d;
        }
    }

    private static readonly Dictionary<Action, KeyCode> Defaults = new()
    {
        { Action.MoveLeft, KeyCode.A },
        { Action.MoveRight, KeyCode.D },
        { Action.MoveUp, KeyCode.W },
        { Action.MoveDown, KeyCode.S },

        { Action.Jump, KeyCode.Space },
        { Action.Dash, KeyCode.LeftShift },
        { Action.Attack, KeyCode.J },
        { Action.CounterAttack, KeyCode.K },
        { Action.PlatformDrop, KeyCode.S },

        { Action.SkillSlot1, KeyCode.H },
        { Action.SkillSlot2, KeyCode.Y },
        { Action.SkillSlot3, KeyCode.U },
        { Action.SkillSlot4, KeyCode.I },
        { Action.SkillSlot5, KeyCode.O },
        { Action.DomainExpansion, KeyCode.O },

        { Action.ToggleCharacterPanel, KeyCode.Tab },
        { Action.ToggleSkillPanel, KeyCode.L },
        { Action.ToggleSettingsPanel, KeyCode.N },
        { Action.ToggleQuestPanel, KeyCode.M },

        { Action.Interact, KeyCode.F },
        { Action.Escape, KeyCode.Escape },
    };

    private static void LoadBindings()
    {
        string saved = PlayerPrefs.GetString(PlayerPrefsKey, "");
        s_bindings = new Dictionary<Action, KeyCode>(Defaults);

        if (string.IsNullOrEmpty(saved))
            return;

        string[] pairs = saved.Split(';');
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(':');
            if (parts.Length == 2 &&
                System.Enum.TryParse(parts[0], out Action action) &&
                System.Enum.TryParse(parts[1], out KeyCode key))
            {
                s_bindings[action] = key;
            }
        }
    }

    private static void SaveBindings()
    {
        var list = new List<string>();
        foreach (var kvp in s_bindings)
            list.Add($"{kvp.Key}:{kvp.Value}");
        PlayerPrefs.SetString(PlayerPrefsKey, string.Join(";", list));
        PlayerPrefs.Save();
    }
}
