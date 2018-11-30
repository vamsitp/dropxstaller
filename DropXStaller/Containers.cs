using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DropXStaller
{
    public class Containers
    {
        public int count { get; set; }
        public Value[] value { get; set; }
    }

    public class Value
    {
        public int id { get; set; }
        public string scopeIdentifier { get; set; }
        public string artifactUri { get; set; }
        public string securityToken { get; set; }
        public string name { get; set; }
        public int size { get; set; }
        public string createdBy { get; set; }
        public DateTime dateCreated { get; set; }
        public string itemLocation { get; set; }
        public string contentLocation { get; set; }
    }
}
