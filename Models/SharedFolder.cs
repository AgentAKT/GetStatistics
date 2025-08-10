using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogNavigator.Models
{
    public class SharedFolder
    {
        public string ServerName { get; set; }
        public string SharePath { get; set; }
        public string LocalMountPoint { get; set; }
        public DateTime LastAccessed { get; set; }
    }
}
