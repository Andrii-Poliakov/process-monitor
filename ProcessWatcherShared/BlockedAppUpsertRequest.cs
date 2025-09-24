using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessWatcherShared
{
    public class BlockedAppUpsertRequest
    {
        public int BlockType { get; set; }
        public string BlockValue { get; set; } = string.Empty;
    }
}
