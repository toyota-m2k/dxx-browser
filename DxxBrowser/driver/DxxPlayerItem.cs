using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    public class DxxPlayItem : IDxxPlayItem {
        private Uri Uri;
        public string SourceUrl => Uri.ToString();
        public string FilePath => DxxDriverManager.Instance.FindDriver(SourceUrl)?.StorageManager?.GetSavedFile(Uri);

        public DxxPlayItem(Uri uri) {
            Uri = uri;
        }

        public static DxxPlayItem FromUrl(string url) {
            return new DxxPlayItem(new Uri(url));
        }
    }
}
