using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Antlr4.Runtime;

namespace tsql2pgsql
{
    class CaseInsensitiveStream : AntlrInputStream
    {
        public CaseInsensitiveStream(String s)
            : base(new StringReader(s))
        {
        }

        public override int La(int i)
        {
            if (i == 0)
            {
                return 0; // undefined
            }
            if (i < 0)
            {
                i++; // e.g., translate LA(-1) to use offset i=0; then data[p+0-1]
                if ((p + i - 1) < 0)
                {
                    return IntStreamConstants.Eof;
                }
            }
            // invalid; no char before first char
            if ((p + i - 1) >= n)
            {
                return (int)IntStreamConstants.Eof;
            }
            return Char.ToLower(data[p + i - 1]);
        }
    }
}
