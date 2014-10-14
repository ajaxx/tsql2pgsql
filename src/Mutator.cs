using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Nito.KitchenSink.OptionParsing;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using Common.Logging;

namespace tsql2pgsql
{
    public class Mutator
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static ILog _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Application options
        /// </summary>
        private static AppOptions _options;

        static void MutateFile(string filePath)
        {
            Console.WriteLine("> {0}", filePath);

            var source = File.ReadAllLines(filePath);
            var mutation = new MutationVisitor(source);
            mutation.Mutate();
        }

        static void Main(string[] args)
        {
            try {
				_options = OptionParser.Parse<AppOptions>();

                // determine the number of threads to use while processing
				if (_options.Threads == 0)
					_options.Threads = Environment.ProcessorCount;

                _options.Files.ForEach(MutateFile);

                Console.WriteLine();
            } catch( OptionParsingException e ) {
				Console.Error.WriteLine(e.Message);
				AppOptions.Usage();
			}
        }
    }
}
