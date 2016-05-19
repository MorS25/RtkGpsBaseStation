using System.Collections.Generic;

namespace RtkGpsBase{
    public sealed class HttpRequest
    {
        public string Method { get; set; }

        public string URL { get; set; }

        public string Version { get; set; }

        public IEnumerable<KeyValuePair<string, string>> UrlParameters { get; set; }

        public bool Execute { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }

        public int BodySize { get; set; }

        public string BodyContent { get; set; }
    }
}