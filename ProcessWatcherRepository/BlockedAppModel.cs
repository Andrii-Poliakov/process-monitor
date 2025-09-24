using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessMonitorRepository
{
    public class BlockedAppModel
    {
        public int Id { get; set; }
        public int BlockType { get; set; }
        public string BlockTypeName { get; set; } = string.Empty;
        public string BlockValue { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class BlockTypeModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
