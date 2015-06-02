using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace findmagic
{
    class DefinitionFile
    {
        public List<Subroutine> Subroutines { get; set; }

        public List<Subroutine> AmbigousSubroutines { get; set; }

        public List<Subroutine> Unmatchable { get; set; }
    }
}
