using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessWatcherShared
{
    public class BlockedAppDto
    {
        public int Id { get; set; }
        public int BlockType { get; set; }
        public string BlockTypeName { get; set; } = string.Empty;
        public string BlockValue { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
