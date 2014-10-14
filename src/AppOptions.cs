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
        }

		[Option("threads", 't')]
		public int Threads { get; set; }

        [Option("echo", OptionArgument.None)]
        public bool Echo { get; set; }
        [Option("tokens", OptionArgument.None)]
        public bool Tokens { get; set; }

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
