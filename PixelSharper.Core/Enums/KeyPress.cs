namespace PixelSharper.Core.Enums;

/// <summary>
/// Keyboard key codes recognised by the engine.
/// </summary>
/// <remarks>
/// Port of olc::Key. These values double as PixelSharper's own keycodes,
/// so the platform layer maps native keys directly onto this enum.
/// </remarks>
public enum KeyPress
{
    /// <summary>No key / unbound.</summary>
    NONE,
    /// <summary>The A key.</summary>
    A,
    /// <summary>The B key.</summary>
    B,
    /// <summary>The C key.</summary>
    C,
    /// <summary>The D key.</summary>
    D,
    /// <summary>The E key.</summary>
    E,
    /// <summary>The F key.</summary>
    F,
    /// <summary>The G key.</summary>
    G,
    /// <summary>The H key.</summary>
    H,
    /// <summary>The I key.</summary>
    I,
    /// <summary>The J key.</summary>
    J,
    /// <summary>The K key.</summary>
    K,
    /// <summary>The L key.</summary>
    L,
    /// <summary>The M key.</summary>
    M,
    /// <summary>The N key.</summary>
    N,
    /// <summary>The O key.</summary>
    O,
    /// <summary>The P key.</summary>
    P,
    /// <summary>The Q key.</summary>
    Q,
    /// <summary>The R key.</summary>
    R,
    /// <summary>The S key.</summary>
    S,
    /// <summary>The T key.</summary>
    T,
    /// <summary>The U key.</summary>
    U,
    /// <summary>The V key.</summary>
    V,
    /// <summary>The W key.</summary>
    W,
    /// <summary>The X key.</summary>
    X,
    /// <summary>The Y key.</summary>
    Y,
    /// <summary>The Z key.</summary>
    Z,
    /// <summary>The 0 key on the main keyboard.</summary>
    K0,
    /// <summary>The 1 key on the main keyboard.</summary>
    K1,
    /// <summary>The 2 key on the main keyboard.</summary>
    K2,
    /// <summary>The 3 key on the main keyboard.</summary>
    K3,
    /// <summary>The 4 key on the main keyboard.</summary>
    K4,
    /// <summary>The 5 key on the main keyboard.</summary>
    K5,
    /// <summary>The 6 key on the main keyboard.</summary>
    K6,
    /// <summary>The 7 key on the main keyboard.</summary>
    K7,
    /// <summary>The 8 key on the main keyboard.</summary>
    K8,
    /// <summary>The 9 key on the main keyboard.</summary>
    K9,
    /// <summary>The F1 function key.</summary>
    F1,
    /// <summary>The F2 function key.</summary>
    F2,
    /// <summary>The F3 function key.</summary>
    F3,
    /// <summary>The F4 function key.</summary>
    F4,
    /// <summary>The F5 function key.</summary>
    F5,
    /// <summary>The F6 function key.</summary>
    F6,
    /// <summary>The F7 function key.</summary>
    F7,
    /// <summary>The F8 function key.</summary>
    F8,
    /// <summary>The F9 function key.</summary>
    F9,
    /// <summary>The F10 function key.</summary>
    F10,
    /// <summary>The F11 function key.</summary>
    F11,
    /// <summary>The F12 function key.</summary>
    F12,
    /// <summary>The Up arrow key.</summary>
    UP,
    /// <summary>The Down arrow key.</summary>
    DOWN,
    /// <summary>The Left arrow key.</summary>
    LEFT,
    /// <summary>The Right arrow key.</summary>
    RIGHT,
    /// <summary>The Space bar.</summary>
    SPACE,
    /// <summary>The Tab key.</summary>
    TAB,
    /// <summary>The Shift key.</summary>
    SHIFT,
    /// <summary>The Control key.</summary>
    CTRL,
    /// <summary>The Insert key.</summary>
    INS,
    /// <summary>The Delete key.</summary>
    DEL,
    /// <summary>The Home key.</summary>
    HOME,
    /// <summary>The End key.</summary>
    END,
    /// <summary>The Page Up key.</summary>
    PGUP,
    /// <summary>The Page Down key.</summary>
    PGDN,
    /// <summary>The Backspace key.</summary>
    BACK,
    /// <summary>The Escape key.</summary>
    ESCAPE,
    /// <summary>The Return key.</summary>
    RETURN,
    /// <summary>The Enter key (numpad Enter).</summary>
    ENTER,
    /// <summary>The Pause key.</summary>
    PAUSE,
    /// <summary>The Scroll Lock key.</summary>
    SCROLL,
    /// <summary>Numpad 0.</summary>
    NP0,
    /// <summary>Numpad 1.</summary>
    NP1,
    /// <summary>Numpad 2.</summary>
    NP2,
    /// <summary>Numpad 3.</summary>
    NP3,
    /// <summary>Numpad 4.</summary>
    NP4,
    /// <summary>Numpad 5.</summary>
    NP5,
    /// <summary>Numpad 6.</summary>
    NP6,
    /// <summary>Numpad 7.</summary>
    NP7,
    /// <summary>Numpad 8.</summary>
    NP8,
    /// <summary>Numpad 9.</summary>
    NP9,
    /// <summary>Numpad multiply key.</summary>
    NP_MUL,
    /// <summary>Numpad divide key.</summary>
    NP_DIV,
    /// <summary>Numpad add key.</summary>
    NP_ADD,
    /// <summary>Numpad subtract key.</summary>
    NP_SUB,
    /// <summary>Numpad decimal point key.</summary>
    NP_DECIMAL,
    /// <summary>The period key.</summary>
    PERIOD,
    /// <summary>The equals key.</summary>
    EQUALS,
    /// <summary>The comma key.</summary>
    COMMA,
    /// <summary>The minus key.</summary>
    MINUS,
    /// <summary>OEM key 1 (typically semicolon / colon).</summary>
    OEM_1,
    /// <summary>OEM key 2 (typically forward slash / question mark).</summary>
    OEM_2,
    /// <summary>OEM key 3 (typically backtick / tilde).</summary>
    OEM_3,
    /// <summary>OEM key 4 (typically left bracket).</summary>
    OEM_4,
    /// <summary>OEM key 5 (typically backslash / pipe).</summary>
    OEM_5,
    /// <summary>OEM key 6 (typically right bracket).</summary>
    OEM_6,
    /// <summary>OEM key 7 (typically apostrophe / quote).</summary>
    OEM_7,
    /// <summary>OEM key 8 (no GLFW equivalent).</summary>
    OEM_8,
    /// <summary>The Caps Lock key.</summary>
    CAPS_LOCK,
    /// <summary>Marks the end of the enumeration; equals the total key count.</summary>
    ENUM_END
}
