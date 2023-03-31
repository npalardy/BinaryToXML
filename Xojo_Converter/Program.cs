using System;

namespace Xojo_Converter
{
    class Program
    {
        static void Main(string[] args)
        {
            IRBBFConverter converter;

            converter = new RBBFConverter();
            string infile;
            string outfile;

            infile = (args.Length > 0) ? args[0] : null;
            if (null == infile)
            {
                Console.WriteLine("Input file does not exist");
                return;
            }
            outfile = (args.Length > 1) ? args[1] : null;

            converter.ConvertFile(infile, outfile);
        }
    }
}
