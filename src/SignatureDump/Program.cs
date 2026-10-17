using System;

namespace PmxEditorMcp.SignatureDump
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            return CommandRunner.Run(args, Console.Out, Console.Error);
        }
    }
}
