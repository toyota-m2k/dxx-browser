using System;
using System.Diagnostics;

namespace DxxBrowser.driver {
    public class DxxPlayItem : IDxxPlayItem {
        private Uri Uri;
        public string Url => Uri.ToString();
        public string Path { get; private set; }
        public string Description { get; private set; }

        private DxxPlayItem(Uri uri, string path, string description) {
            Uri = uri;
            Description = description;
            Path = path;
        }

        public static DxxPlayItem FromTarget(DxxTargetInfo target) {
            var desc = target.Description;
            if(string.IsNullOrWhiteSpace(desc)) {
                desc = target.Name;
            }
            var path = DxxDriverManager.Instance.FindDriver(target.Uri.ToString())?.StorageManager?.GetSavedFile(target.Uri);
            if(path==null) {
                Debug.WriteLine("null path.");
                return null;
            }
            return new DxxPlayItem(target.Uri, path, desc);
        }
    }
}
