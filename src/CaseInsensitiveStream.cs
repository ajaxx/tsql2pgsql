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
