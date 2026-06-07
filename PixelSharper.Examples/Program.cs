using PixelSharper.Core.Enums;
using PixelSharper.Core.Extensions;
using PixelSharper.Examples;
using PixelSharper.Examples.Scenes;

// The showcase is one window that pages through self-contained tutorial scenes. Add a scene to the
// list and it appears in the navigation. See docs/TUTORIAL.md for the written walkthrough.
var scenes = new IExampleScene[]
{
    new WelcomeScene(),
    new PrimitivesScene(),
    new SpritesScene(),
    new TextScene(),
    new DecalsScene(),
    new InputScene(),
    new PixelModesScene(),
    new TransformedViewScene(),
    new WireframeScene(),
    new Gfx2dScene(),
    new Hw3dScene(),
    new PaletteScene(),
    new Camera2DScene(),
    new Geometry2DScene(),
    new QuadTreeScene(),
    new DataFileScene(),
    new QuickGuiScene(),
    new FinaleScene(),
};

// Pass --autotest to cycle through every scene and exit (a smoke test of all scenes).
var autotest = args.Length > 0 && args[0] == "--autotest";

var showcase = new Showcase(scenes, autotest);
if (showcase.Construct(320, 240, 3, 3, vsync: true) == FileReadCode.OK)
{
    if (!autotest) _ = new SplashScreen(); // auto-hooks: plays the OLC splash before the showcase
    showcase.Start();
}
