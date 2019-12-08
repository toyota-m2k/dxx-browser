using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxUriEx {
        public string Url { get; }
        private Uri mUri = null;
        public Uri Uri {
            get {
                if(null==mUri) {
                    mUri = new Uri(Url);
                }
                return mUri;
            }
        }

        public DxxUriEx(string url) {
            Url = url;
        }
        public DxxUriEx(Uri uri) {
            mUri = uri;
            Url = uri.ToString();
        }
    }
}
