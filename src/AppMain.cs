// --------------------------------------------------------------------------------
// Copyright (c) 2014, XLR8 Development
// --------------------------------------------------------------------------------
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// --------------------------------------------------------------------------------

using System;
using System.IO;

using Nito.KitchenSink.OptionParsing;

using Common.Logging;

namespace tsql2pgsql
{
    using visitors;
    using pipeline;

    public class AppMain
    {
        /// <summary>
        /// Logger for instance
        /// </summary>
        private static ILog _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Application options
        /// </summary>
        private static AppOptions _options;

        /// <summary>
        /// Converts the contents of the file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public static void ConvertFile(string filePath)
        {
            Console.WriteLine("> {0}", filePath);

            var fileContent = File.ReadAllLines(filePath);
            var pipelineDirector = new PipelineDirector(fileContent)
                .Process<ParentheticalRepairVisitor>()
                .Process<VariableNameConverter>()
                .Process<PgsqlConverter>();
            
            foreach (var line in pipelineDirector.Contents)
            {
                Console.WriteLine(line);
            }
        }

        static void Main(string[] args)
        {
            try {
				_options = OptionParser.Parse<AppOptions>();

                // determine the number of threads to use while processing
				if (_options.Threads == 0)
					_options.Threads = Environment.ProcessorCount;

                _options.Files.ForEach(ConvertFile);

                Console.WriteLine();
            } catch( OptionParsingException e ) {
				Console.Error.WriteLine(e.Message);
				AppOptions.Usage();
			}
        }
    }
}
