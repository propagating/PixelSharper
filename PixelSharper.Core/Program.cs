using PixelSharper.Core.Components;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Extensions;
using PixelSharper.Core.Extensions.Wire;
using PixelSharper.Core.Types;
using Gui = PixelSharper.Core.Extensions.QuickGui;

#region license
// License (OLC-3)
// ~~~~~~~~~~~~~~~
//
//     Copyright 2018 - 2022 OneLoneCoder.com
//
//     Redistribution and use in source and binary forms, with or without modification,
//                                                                        are permitted provided that the following conditions are met:
//
// 1. Redistributions or derivations of source code must retain the above copyright
//     notice, this list of conditions and the following disclaimer.
//
// 2. Redistributions or derivative works in binary form must reproduce the above
// copyright notice. This list of conditions and the following	disclaimer must be
// reproduced in the documentation and/or other materials provided with the distribution.
//
// 3. Neither the name of the copyright holder nor the names of its contributors may
//     be used to endorse or promote products derived from this software without specific
//     prior written permission.
//
//     THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS	"AS IS" AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
// OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT
//     SHALL THE COPYRIGHT	HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
//      INCIDENTAL,	SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED
//     TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR
//     BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
//     CONTRACT, STRICT LIABILITY, OR TORT	(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
// ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
// SUCH DAMAGE.
//
//     Links
// ~~~~~
// YouTube:	https://www.youtube.com/javidx9
// https://www.youtube.com/javidx9extra
// Discord:	https://discord.gg/WhwHUMV
// Twitter:	https://www.twitter.com/javidx9
// Twitch:		https://www.twitch.tv/javidx9
// GitHub:		https://www.github.com/onelonecoder
// Homepage:	https://www.onelonecoder.com
// Patreon:	https://www.patreon.com/javidx9
// Community:  https://community.onelonecoder.com


#endregion
namespace PixelSharper.Core
{
    internal class Program
    {
        private static void Main()
        {
            var engine = new PixelSharperEngine();
            if (engine.Construct(256, 240, 4, 4, vsync: true) == FileReadCode.OK)
            {
                engine.Start();
            }
        }
    }
    
    public class PixelSharperEngine : PixelGameEngine
    {
        private float _t;
        private Sprite _sprite;
        private Decal _decal;
        private Model _gear;
        private TransformedView _view;
        private Gui.Manager _gui;
        private readonly System.Collections.Generic.List<string> _guiItems = new() { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };

        private static readonly float[] Identity4x4 =
        {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        };

        public PixelSharperEngine()
        {
            ApplicationName = "Test Application";
            Configuration = new PixelConfiguration(5, 0xFF, 4, 128);
        }

        public override bool OnCreate()
        {
            // Build a small 8x8 sprite (a magenta-bordered checker) to exercise DrawSprite.
            _sprite = new Sprite(8, 8);
            for (var sy = 0; sy < 8; sy++)
            for (var sx = 0; sx < 8; sx++)
            {
                var edge = sx == 0 || sy == 0 || sx == 7 || sy == 7;
                var pix = edge ? Pixel.MAGENTA : ((sx + sy) % 2 == 0 ? Pixel.WHITE : Pixel.DARK_GREY);
                _sprite.SetPixel(sx, sy, pix);
            }

            // A decal wraps the sprite as a GPU texture for the hardware decal path.
            _decal = new Decal(_sprite);
            // (To see the OLC splash, add `_ = new SplashScreen();` here — it auto-hooks.)

            // Wireframe extension: an 8-tooth gear model.
            _gear = new Model();
            _gear.SetMesh(Wireframe.MeshGear(8, 14, 9));

            // TransformedView (pan/zoom camera) covering the screen.
            _view = new TransformedView();
            _view.Initialise(new Vector2d<int>(ScreenWidth(), ScreenHeight()));

            // QuickGUI extension: a small panel (label, button, checkbox, slider, scrollable list).
            _gui = new Gui.Manager();
            _ = new Gui.TextBox(_gui, "type here", new Vector2d<float>(150, 134), new Vector2d<float>(90, 12));
            _ = new Gui.Label(_gui, "QuickGUI", new Vector2d<float>(150, 148), new Vector2d<float>(90, 12)) { HasBorder = true };
            _ = new Gui.Button(_gui, "OK", new Vector2d<float>(150, 162), new Vector2d<float>(40, 14));
            _ = new Gui.CheckBox(_gui, "On", true, new Vector2d<float>(196, 162), new Vector2d<float>(44, 14));
            _ = new Gui.Slider(_gui, new Vector2d<float>(156, 182), new Vector2d<float>(234, 182), 0, 100, 50);
            _ = new Gui.ListBox(_gui, _guiItems, new Vector2d<float>(150, 190), new Vector2d<float>(90, 46));
            return true;
        }

        public override bool OnUpdate(float elapsedTime)
        {
            // Escape closes the application.
            if (GetKey(KeyPress.ESCAPE).Pressed)
                return false;

            _t += elapsedTime;
            _gui.Update(this);
            Clear(Pixel.VERY_DARK_BLUE);

            // Primitives showcase
            FillRect(10, 10, 40, 30, Pixel.DARK_RED);
            DrawRect(10, 10, 40, 30, Pixel.RED);
            FillCircle(120, 60, 25, Pixel.DARK_GREEN);
            DrawCircle(120, 60, 25, Pixel.GREEN);
            FillTriangle(190, 20, 230, 90, 160, 90, Pixel.DARK_CYAN);
            DrawTriangle(190, 20, 230, 90, 160, 90, Pixel.CYAN);

            // A spinning line (dashed via the bit pattern)
            var cx = 128 + (int)(Math.Cos(_t) * 60);
            var cy = 180 + (int)(Math.Sin(_t) * 30);
            DrawLine(128, 180, cx, cy, Pixel.YELLOW, 0xF0F0F0F0);

            // Draw the sprite scaled, flipped horizontally
            DrawSprite(60, 150, _sprite, 4, SpriteMirrorMode.Horizontal);

            // TransformedView: middle-drag to pan, wheel to zoom. World draws go through it.
            _view.HandlePanAndZoom();
            _view.DrawCircle(new Vector2d<float>(200, 150), 8, Pixel.GREEN);

            // Wireframe drawn via the view-aware overload (world-space, respects pan/zoom).
            _gear.SetPosition(new Vector2d<float>(225, 60));
            _gear.SetRotation(_t);
            _gear.UpdateInWorld(new Matrix2D());
            Wireframe.DrawModel(_view, _gear, Pixel.WHITE);

            // GFX2D extension: affine-transform blit of the sprite (centre, spin, scale, place).
            var tf = new GFX2D.Transform2D();
            tf.Translate(-4, -4);
            tf.Rotate(_t * 1.5f);
            tf.Scale(3, 3);
            tf.Translate(30, 205);
            GFX2D.DrawSprite(_sprite, tf);

            // Software textured triangle sampling the sprite, and a sprite patch (textured quad).
            FillTexturedTriangle(
                new[] { new Vector2d<float>(100, 55), new Vector2d<float>(140, 95), new Vector2d<float>(100, 95) },
                new[] { new Vector2d<float>(0.5f, 0f), new Vector2d<float>(1f, 1f), new Vector2d<float>(0f, 1f) },
                new[] { Pixel.WHITE, Pixel.WHITE, Pixel.WHITE }, _sprite);
            DrawSprite(new Vector2d<float>(100, 25), _sprite.ToSpritePatch(), new Vector2d<float>(24, 24));

            // GPU decal path: a scaled decal, a spinning rotated decal, a translucent rect, text.
            DrawDecal(new Vector2d<float>(170, 18), _decal, new Vector2d<float>(3, 3));
            DrawRotatedDecal(new Vector2d<float>(215, 165), _decal, _t * 2,
                new Vector2d<float>(4, 4), new Vector2d<float>(5, 5), Pixel.YELLOW);
            FillRectDecal(new Vector2d<float>(150, 188), new Vector2d<float>(70, 18), new Pixel(0, 128, 255, 128));
            DrawStringDecal(new Vector2d<float>(152, 192), "decal text", Pixel.WHITE);
            DrawDecal(new Vector2d<float>(120, 20), _decal.ToDecalPatch(), new Vector2d<float>(20, 20));

            // QuickGUI panel (GPU decal path).
            _gui.DrawDecal(this);

            // HW3D: a wireframe box (depth off => drawn directly in NDC, top-left corner).
            HW3D_EnableDepthTest(false);
            HW3D_DrawLineBox(Identity4x4,
                new float[] { -0.95f, 0.65f, 0f, 0f }, new float[] { 0.3f, 0.25f, 0.1f, 0f }, Pixel.MAGENTA);

            // Text: monospaced + proportional + scaled.
            DrawString(6, 100, "PixelSharper", Pixel.WHITE);
            DrawStringProp(6, 112, "Proportional font!", Pixel.TANGERINE);
            DrawString(6, 124, $"Mouse: {GetMouseX()},{GetMouseY()}", Pixel.CYAN);

            // Drag a file onto the window to see its path.
            var files = GetDroppedFiles();
            if (files.Count > 0)
                DrawString(6, 136, $"Dropped: {System.IO.Path.GetFileName(files[0])}", Pixel.GREEN);
            DrawString(6, 210, "ESC to quit", Pixel.GREY, 2);

            // White cursor box at the mouse, red while the left button is held.
            var cursor = GetMouse((int)Mouse.Left).Held ? Pixel.RED : Pixel.WHITE;
            FillRect(GetMouseX() - 2, GetMouseY() - 2, 5, 5, cursor);

            return true;
        }
    }
    
}

