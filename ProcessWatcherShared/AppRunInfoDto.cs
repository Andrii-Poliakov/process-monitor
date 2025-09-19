using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessWatcherShared
{
    public class AppRunInfoDto
    {
        public int Id { get; set; }
        public int AppId { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int Status { get; set; }
    }
}
