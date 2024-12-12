using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace Bacen_v2.Utils
{
    public static class ConfigLoader
    {
        public static dynamic Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<dynamic>(json)!;
        }
    }
}