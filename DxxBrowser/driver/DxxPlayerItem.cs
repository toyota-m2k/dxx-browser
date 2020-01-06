using System;

namespace DxxBrowser.driver {
    public class DxxPlayItem : IDxxPlayItem {
        private Uri Uri;
        public string SourceUrl => Uri.ToString();
        public string FilePath => DxxDriverManager.Instance.FindDriver(SourceUrl)?.StorageManager?.GetSavedFile(Uri);
        public string Description { get; private set; }

        private DxxPlayItem(Uri uri, string description) {
            Uri = uri;
            Description = description;
        }

        public static DxxPlayItem FromTarget(DxxTargetInfo target) {
            var desc = target.Description;
            if(string.IsNullOrWhiteSpace(desc)) {
                desc = target.Name;
            }
            return new DxxPlayItem(target.Uri, desc);
        }
    }
}
