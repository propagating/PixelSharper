using System.Numerics;
using System.Runtime.CompilerServices;
using PixelSharper.Core.Enums;
using PixelSharper.Core.Resources;
using PixelSharper.Core.Types;

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
            if (engine.Construct(1, 1, 1, 1))
            {
                engine.Start();
            }

            var resources = new ResourcePack();
            var directory = new DirectoryInfo("E:\\Projects\\C#\\PixelSharper\\PixelSharper.Core");
            foreach (var file in directory.GetFiles())
            {
                resources.AddFileToPack(file.FullName);
            }

            resources.SaveResourcePack("C:\\Users\\Ryan\\Desktop\\testpack_encrypted.txt", ResourcePackProtectionMode.Encrypted, "this is a test key");   
            resources.LoadResourcePack("C:\\Users\\Ryan\\Desktop\\testpack_encrypted.txt", ResourcePackProtectionMode.Encrypted, "this is a test key");
            
            
            foreach (var file in resources.FileMap)
            {
                Console.WriteLine(file.Key);
                var buffer = resources.GetFileBuffer(file.Key);
                Console.WriteLine(buffer.Buffer.Length);

                var fileInfo = new FileInfo(file.Key);
                using (var fs = new FileStream($"C:\\Users\\Ryan\\Desktop\\{fileInfo.Name}", FileMode.OpenOrCreate))
                {
                    fs.Write(buffer.Buffer, 0, buffer.Buffer.Length);
                }
                
                Console.WriteLine("wrote file to disk");
                
            }
            
            resources.SaveResourcePack("C:\\Users\\Ryan\\Desktop\\testpack_unencrypted.txt", ResourcePackProtectionMode.None, "this is a test key");

            
            Console.Read();
        }
    }
    
    public class PixelSharperEngine : CoreEngine
    {

        public PixelSharperEngine()
        {
            ApplicationName = "Test Application";
            Configuration = new PixelConfiguration(5, 0xFF, 4, 128);
        }
        public override bool OnCreate()
        {
            return true;
        }

        public override bool OnUpdate(float elapsedTime)
        {
            return true;
        }
    }
    
}

