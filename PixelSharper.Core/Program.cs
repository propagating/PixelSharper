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

