using System;
using UnityEngine.InputSystem;

// The two ways to play, and what each of the ronin's moves is pressed with in them.
//   Keyboard: one hand moves, the other attacks, in either of two layouts that work together:
//     arrows + SPACE (slash) / C (parry) / X (shuriken) / Z (ultimate), the arcade layout the prompts show, or
//     A D + J (slash) / K (parry) / L (shuriken) / I (ultimate).
//   Keyboard + Mouse: A D move, hold left click to slash, right click parries, SPACE throws the shuriken and E (or a
//     middle click) unleashes the ultimate.
// The choice is a setting (PlayerPrefs, not the save: it belongs to the player, not the journey), and the fight
// switches it by itself when the other scheme's own buttons are used, so the prompts always show what's in hand.
public static class Controls
{
    public enum Scheme { Keyboard, KeyboardMouse }

    const string SchemeKey = "ControlScheme";
    static Scheme? current;

    public static Scheme Current
    {
        get => current ??= (Scheme)UnityEngine.PlayerPrefs.GetInt(SchemeKey, (int)Scheme.Keyboard);
        set
        {
            if (current == value) return;
            current = value;
            UnityEngine.PlayerPrefs.SetInt(SchemeKey, (int)value);
            Changed?.Invoke();
        }
    }

    public static bool UsesMouse => Current == Scheme.KeyboardMouse;
    // Raised when the scheme changes, so the HUD's key tips can follow.
    public static event Action Changed;

    public static string Name(Scheme scheme) => scheme == Scheme.Keyboard ? "KEYBOARD" : "KEYBOARD + MOUSE";

    // Left and right on the fight's row: both layouts' keys, whichever scheme is on.
    public static float MoveX(Keyboard keyboard)
    {
        float x = 0f;
        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) x += 1f;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) x -= 1f;
        return x;
    }

    // In a fight: the mouse's buttons switch to Keyboard + Mouse, a key only Keyboard uses switches back. SPACE is
    // both schemes' (slash, or shuriken), so it never switches.
    public static void Detect(Keyboard keyboard, Mouse mouse)
    {
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
            Current = Scheme.KeyboardMouse;
        else if (keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame
            || keyboard.jKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame
            || keyboard.iKey.wasPressedThisFrame))
            Current = Scheme.Keyboard;
    }

    // Held, so the slash repeats at its own pace.
    public static bool Slash(Keyboard keyboard, Mouse mouse) => UsesMouse
        ? mouse != null && mouse.leftButton.isPressed
        : keyboard.spaceKey.isPressed || keyboard.jKey.isPressed;

    public static bool Parry(Keyboard keyboard, Mouse mouse) => UsesMouse
        ? mouse != null && mouse.rightButton.wasPressedThisFrame
        : keyboard.cKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame;

    public static bool Shuriken(Keyboard keyboard, Mouse mouse) => UsesMouse
        ? keyboard.spaceKey.wasPressedThisFrame
        : keyboard.xKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame;

    public static bool Ultimate(Keyboard keyboard, Mouse mouse) => UsesMouse
        ? keyboard.eKey.wasPressedThisFrame || (mouse != null && mouse.middleButton.wasPressedThisFrame)
        : keyboard.zKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame;

    // A move's buttons in the current scheme, as drawn key caps (Poguru's lessons): every binding, so both keyboard
    // layouts see theirs.
    public static KeyGlyph[] Glyphs(Moves move) => AllGlyphs(move, Current);

    // Every binding of a move in a scheme as drawn caps, "or" between alternatives (the new journey's cards).
    public static KeyGlyph[] AllGlyphs(Moves move, Scheme scheme)
    {
        var glyphs = new System.Collections.Generic.List<KeyGlyph>();
        foreach (string binding in Bindings(move, scheme))
        {
            if (glyphs.Count > 0) glyphs.Add(KeyGlyph.Or);
            glyphs.Add(binding switch
            {
                "LEFT CLICK" => KeyGlyph.MouseLeft,
                "RIGHT CLICK" => KeyGlyph.MouseRight,
                "MIDDLE CLICK" => KeyGlyph.MouseMiddle,
                "SPACE" => KeyGlyph.Space,
                _ => KeyGlyph.Key(binding),
            });
        }
        return glyphs.ToArray();
    }

    // Left and right as drawn caps: A D, and the arrows too on the keyboard.
    public static KeyGlyph[] MoveGlyphs(Scheme scheme) => scheme == Scheme.KeyboardMouse
        ? new[] { KeyGlyph.Key("A"), KeyGlyph.Key("D") }
        : new[] { KeyGlyph.Key("←"), KeyGlyph.Key("→"), KeyGlyph.Or, KeyGlyph.Key("A"), KeyGlyph.Key("D") };

    // The same as words for a key cap in text (the HUD's Tab tips): only the main button.
    public static string Label(Moves move) => UsesMouse
        ? move switch { Moves.Slash => "Left Click", Moves.Shuriken => "Space", Moves.Parry => "Right Click", _ => "E" }
        : move switch { Moves.Slash => "Space", Moves.Shuriken => "X", Moves.Parry => "C", _ => "Z" };

    // Every binding of a move in a scheme (the current one by default), for the controls page and the new journey's
    // choice: "or" between alternatives.
    public static string[] Bindings(Moves move) => Bindings(move, Current);

    public static string[] Bindings(Moves move, Scheme scheme) => scheme == Scheme.KeyboardMouse
        ? move switch
        {
            Moves.Slash => new[] { "LEFT CLICK" },
            Moves.Shuriken => new[] { "SPACE" },
            Moves.Parry => new[] { "RIGHT CLICK" },
            _ => new[] { "E", "MIDDLE CLICK" },
        }
        : move switch
        {
            Moves.Slash => new[] { "SPACE", "J" },
            Moves.Shuriken => new[] { "X", "L" },
            Moves.Parry => new[] { "C", "K" },
            _ => new[] { "Z", "I" },
        };
}
