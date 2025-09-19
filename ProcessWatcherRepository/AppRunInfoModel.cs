using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessMonitorRepository
{
    public class AppRunInfoModel
    {
        public int Id { get; set; }
        public int AppId { get; set; }
        public string StartUtc { get; set; }
        public string EndUtc { get; set; }

    }
}
