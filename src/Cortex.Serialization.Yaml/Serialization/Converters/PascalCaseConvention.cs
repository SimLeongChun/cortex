using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cortex.Serialization.Yaml.Converters
{
    public sealed class PascalCaseConvention : INamingConvention
    {
        public string Convert(string n) => char.ToUpperInvariant(n[0]) + n[1..];
    }
}
