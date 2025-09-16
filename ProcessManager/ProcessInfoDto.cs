using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessManager
{
    public class ProcessInfoDto
    {
        public int ProcessId { get; set; }
        public string Name { get; set; } = null!;
        public string FullPath { get; set; } = null!;
        public DateTime StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }

    }
}
