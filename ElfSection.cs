using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace findmagic
{
    class ElfSection
    {
        public long VirtualAddress { get; set; }
        public long FileAddress { get; set; }
        public long Size { get; set; }

        public string Name { get; set; }

        // I don't care for the rest of the properties

        public long MapAddress(long toMap)
        {
            return FileAddress + (toMap - VirtualAddress);
        }
    }
}
