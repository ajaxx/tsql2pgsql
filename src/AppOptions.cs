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
using System.Collections.Generic;

using Nito.KitchenSink.OptionParsing;

namespace tsql2pgsql
{
	public sealed class AppOptions : IOptionArguments
	{
        public AppOptions()
        {
            Files = new List<string>();
            OutputDir = Environment.CurrentDirectory;
            Extension = ".pgsql";
        }

		[Option("threads", 't')]
		public int Threads { get; set; }

        [Option("echo", OptionArgument.None)]
        public bool Echo { get; set; }
        [Option("tokens", OptionArgument.None)]
        public bool Tokens { get; set; }

        [Option("outdir", OptionArgument.Required)]
        public string OutputDir { get; set; }

        [Option("extension", OptionArgument.Required)]
        public string Extension { get; set; }

        [PositionalArguments]
        public List<string> Files { get; private set; }

        public void Validate()
        {
        }

		public static int Usage()
		{
			Console.Error.WriteLine("Usage: app [OPTIONS] ...");
			return -1;		 	
		}
	}
}
