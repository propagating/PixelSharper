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
    new PerfMonitorScene(),
    new FinaleScene(),
};

// Pass --autotest to cycle through every scene and exit (a smoke test of all scenes).
// Pass --ogl10 to drive the showcase with the legacy fixed-function renderer instead of the default OGL33.
var autotest = args.Contains("--autotest");
var useOgl10 = args.Contains("--ogl10");

var showcase = new Showcase(scenes, autotest, useOgl10);
if (showcase.Construct(640, 480, 1, 1, vsync: true) == FileReadCode.Ok)
{
    if (!autotest) _ = new SplashScreen(); // auto-hooks: plays the OLC splash before the showcase
    showcase.Start();
}
