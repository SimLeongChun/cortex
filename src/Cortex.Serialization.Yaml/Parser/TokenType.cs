using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cortex.Serialization.Yaml.Parser
{
    internal enum TokenType
    {
        Scalar,
        Key,
        Dash,
        NewLine,
        Indent,
        Dedent,
        BlockLiteral,
        BlockFolded,
        EOF
    }

}
