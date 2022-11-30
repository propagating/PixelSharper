using PixelSharper.Core.Resources;

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

            resources.SaveResourcePack("C:\\Users\\Ryan\\Desktop\\testpack.txt", true);

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

