namespace PixelSharper.Core.Components;

/// <summary>
/// Per-frame button state edges (pressed / released / held). Port of olc::HWButton.
/// </summary>
/// <remarks>
/// Derived each frame from the raw new / old hardware state by the engine's input scan, exactly as
/// in olc. Held in arrays so element-field writes mutate in place.
/// </remarks>
public struct HardwareButton
{
    /// <summary>True on the single frame the button transitioned from up to down.</summary>
    public bool Pressed;

    /// <summary>True on the single frame the button transitioned from down to up.</summary>
    public bool Released;

    /// <summary>True for every frame the button is currently down.</summary>
    public bool Held;
}
