namespace KeyStrokes.Services;

/// <summary>One physical key on the rendered virtual keyboard.</summary>
public readonly record struct KeyDef(int Vk, string Face, double Width, bool IsSpacer = false)
{
    public static KeyDef Spacer(double width) => new(-1, string.Empty, width, true);
}

/// <summary>A high-level bucket used for grouping in the breakdown view.</summary>
public enum KeyCategory
{
    Letters,
    Numbers,
    Symbols,
    Whitespace,
    Modifiers,
    Navigation,
    Function,
    Numpad,
    Media,
    Other,
}

/// <summary>
/// Translates raw virtual-key codes into human-friendly names/categories and
/// describes the physical layout used to draw the heatmap keyboard. Pure data +
/// lookups — no state, safe to call from any thread.
/// </summary>
public static class KeyMapper
{
    private static readonly Dictionary<int, string> Names = BuildNames();

    public static string FriendlyName(int vk)
    {
        if (Names.TryGetValue(vk, out var name)) return name;
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();          // A-Z
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();          // 0-9
        return $"Key {vk} (0x{vk:X2})";
    }

    /// <summary>Compact face label used on the heatmap keycaps.</summary>
    public static string Face(int vk) => vk switch
    {
        0x08 => "⌫",   // Backspace
        0x09 => "Tab",
        0x0D => "Enter",
        0x14 => "Caps",
        0x1B => "Esc",
        0x20 => "Space",
        0xA0 => "Shift",
        0xA1 => "Shift",
        0xA2 => "Ctrl",
        0xA3 => "Ctrl",
        0xA4 => "Alt",
        0xA5 => "Alt",
        0x5B => "⊞",   // Win
        0x5D => "≡",   // Menu
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        _ => Names.TryGetValue(vk, out var n) && n.Length <= 5 ? n : ShortName(vk),
    };

    private static string ShortName(int vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        return vk switch
        {
            0xBA => ";", 0xBB => "=", 0xBC => ",", 0xBD => "-", 0xBE => ".",
            0xBF => "/", 0xC0 => "`", 0xDB => "[", 0xDC => "\\", 0xDD => "]", 0xDE => "'",
            0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4", 0x74 => "F5", 0x75 => "F6",
            0x76 => "F7", 0x77 => "F8", 0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
            _ => "?",
        };
    }

    public static KeyCategory Category(int vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return KeyCategory.Letters;
        if (vk >= 0x30 && vk <= 0x39) return KeyCategory.Numbers;
        if (vk >= 0x60 && vk <= 0x6F) return KeyCategory.Numpad;
        if (vk >= 0x70 && vk <= 0x87) return KeyCategory.Function;

        return vk switch
        {
            0x20 or 0x0D or 0x09 or 0x08 => KeyCategory.Whitespace,
            0x10 or 0x11 or 0x12 or 0x14 or 0x5B or 0x5C or 0x5D
                or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 => KeyCategory.Modifiers,
            0x1B or 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
                or 0x2D or 0x2E or 0x2C or 0x90 or 0x91 => KeyCategory.Navigation,
            0xAD or 0xAE or 0xAF or 0xB0 or 0xB1 or 0xB2 or 0xB3 => KeyCategory.Media,
            0xBA or 0xBB or 0xBC or 0xBD or 0xBE or 0xBF or 0xC0
                or 0xDB or 0xDC or 0xDD or 0xDE => KeyCategory.Symbols,
            _ => KeyCategory.Other,
        };
    }

    // ---------------------------------------------------------------------
    // Physical layout for the heatmap. Widths are in "key units" (1.0 = a
    // standard letter key). A spacer inserts an empty gap of the given width.
    // ---------------------------------------------------------------------
    public static IReadOnlyList<IReadOnlyList<KeyDef>> KeyboardLayout { get; } = new List<IReadOnlyList<KeyDef>>
    {
        new List<KeyDef>
        {
            new(0x1B, "Esc", 1), KeyDef.Spacer(0.5),
            new(0x70, "F1", 1), new(0x71, "F2", 1), new(0x72, "F3", 1), new(0x73, "F4", 1), KeyDef.Spacer(0.4),
            new(0x74, "F5", 1), new(0x75, "F6", 1), new(0x76, "F7", 1), new(0x77, "F8", 1), KeyDef.Spacer(0.4),
            new(0x78, "F9", 1), new(0x79, "F10", 1), new(0x7A, "F11", 1), new(0x7B, "F12", 1),
        },
        new List<KeyDef>
        {
            new(0xC0, "`", 1), new(0x31, "1", 1), new(0x32, "2", 1), new(0x33, "3", 1), new(0x34, "4", 1),
            new(0x35, "5", 1), new(0x36, "6", 1), new(0x37, "7", 1), new(0x38, "8", 1), new(0x39, "9", 1),
            new(0x30, "0", 1), new(0xBD, "-", 1), new(0xBB, "=", 1), new(0x08, "⌫", 2),
        },
        new List<KeyDef>
        {
            new(0x09, "Tab", 1.5), new(0x51, "Q", 1), new(0x57, "W", 1), new(0x45, "E", 1), new(0x52, "R", 1),
            new(0x54, "T", 1), new(0x59, "Y", 1), new(0x55, "U", 1), new(0x49, "I", 1), new(0x4F, "O", 1),
            new(0x50, "P", 1), new(0xDB, "[", 1), new(0xDD, "]", 1), new(0xDC, "\\", 1.5),
        },
        new List<KeyDef>
        {
            new(0x14, "Caps", 1.75), new(0x41, "A", 1), new(0x53, "S", 1), new(0x44, "D", 1), new(0x46, "F", 1),
            new(0x47, "G", 1), new(0x48, "H", 1), new(0x4A, "J", 1), new(0x4B, "K", 1), new(0x4C, "L", 1),
            new(0xBA, ";", 1), new(0xDE, "'", 1), new(0x0D, "Enter", 2.25),
        },
        new List<KeyDef>
        {
            new(0xA0, "Shift", 2.25), new(0x5A, "Z", 1), new(0x58, "X", 1), new(0x43, "C", 1), new(0x56, "V", 1),
            new(0x42, "B", 1), new(0x4E, "N", 1), new(0x4D, "M", 1), new(0xBC, ",", 1), new(0xBE, ".", 1),
            new(0xBF, "/", 1), new(0xA1, "Shift", 2.75),
        },
        new List<KeyDef>
        {
            new(0xA2, "Ctrl", 1.4), new(0x5B, "⊞", 1.2), new(0xA4, "Alt", 1.2),
            new(0x20, "Space", 6.4),
            new(0xA5, "Alt", 1.2), new(0x5D, "≡", 1.2), new(0xA3, "Ctrl", 1.4),
        },
    };

    private static Dictionary<int, string> BuildNames() => new()
    {
        [0x08] = "Backspace",
        [0x09] = "Tab",
        [0x0D] = "Enter",
        [0x13] = "Pause",
        [0x14] = "Caps Lock",
        [0x1B] = "Escape",
        [0x20] = "Space",
        [0x21] = "Page Up",
        [0x22] = "Page Down",
        [0x23] = "End",
        [0x24] = "Home",
        [0x25] = "Left Arrow",
        [0x26] = "Up Arrow",
        [0x27] = "Right Arrow",
        [0x28] = "Down Arrow",
        [0x2C] = "Print Screen",
        [0x2D] = "Insert",
        [0x2E] = "Delete",
        [0x10] = "Shift",
        [0x11] = "Ctrl",
        [0x12] = "Alt",
        [0x5B] = "Left Win",
        [0x5C] = "Right Win",
        [0x5D] = "Menu",
        [0x90] = "Num Lock",
        [0x91] = "Scroll Lock",
        [0xA0] = "Left Shift",
        [0xA1] = "Right Shift",
        [0xA2] = "Left Ctrl",
        [0xA3] = "Right Ctrl",
        [0xA4] = "Left Alt",
        [0xA5] = "Right Alt",
        [0x60] = "Numpad 0",
        [0x61] = "Numpad 1",
        [0x62] = "Numpad 2",
        [0x63] = "Numpad 3",
        [0x64] = "Numpad 4",
        [0x65] = "Numpad 5",
        [0x66] = "Numpad 6",
        [0x67] = "Numpad 7",
        [0x68] = "Numpad 8",
        [0x69] = "Numpad 9",
        [0x6A] = "Numpad *",
        [0x6B] = "Numpad +",
        [0x6C] = "Numpad Separator",
        [0x6D] = "Numpad -",
        [0x6E] = "Numpad .",
        [0x6F] = "Numpad /",
        [0x70] = "F1",
        [0x71] = "F2",
        [0x72] = "F3",
        [0x73] = "F4",
        [0x74] = "F5",
        [0x75] = "F6",
        [0x76] = "F7",
        [0x77] = "F8",
        [0x78] = "F9",
        [0x79] = "F10",
        [0x7A] = "F11",
        [0x7B] = "F12",
        [0xBA] = "Semicolon ;",
        [0xBB] = "Equals =",
        [0xBC] = "Comma ,",
        [0xBD] = "Minus -",
        [0xBE] = "Period .",
        [0xBF] = "Slash /",
        [0xC0] = "Backtick `",
        [0xDB] = "Left Bracket [",
        [0xDC] = "Backslash \\",
        [0xDD] = "Right Bracket ]",
        [0xDE] = "Quote '",
        [0xAD] = "Volume Mute",
        [0xAE] = "Volume Down",
        [0xAF] = "Volume Up",
        [0xB0] = "Next Track",
        [0xB1] = "Previous Track",
        [0xB2] = "Stop Media",
        [0xB3] = "Play/Pause",
    };
}
