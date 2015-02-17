using System.Collections.Generic;

namespace Hyddan.Test.Load
{
    public class Configuration
    {
        public int Executions { get; set; }
        public int Requests { get; set; }
        public int Timeout { get; set; }

        public string Condition { get; set; }
        public string Data { get; set; }
        public string File { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        
        public List<string> Headers { get; set; }
    }
}
