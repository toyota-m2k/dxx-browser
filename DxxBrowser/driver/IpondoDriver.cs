using Common;
using DxxBrowser.driver.dmm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser.driver.ipondo {
    public class IpondoDriver : DxxDriverBaseStoragePathSupport {
        public override string Name => "1Pondo";

        public override string ID => "ipondoDBBasedDriver";

        public override IDxxLinkExtractor LinkExtractor { get; } = new Extractor();
        public override IDxxStorageManager StorageManager { get; } = new Storage();

        public override string ReserveFilePath(Uri uri) {
            return ((DxxFileBasedStorage)StorageManager).GetPath(uri);
        }
        public override bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("1pondo.tv");
        }   
        public override string GetNameFromUri(Uri uri, string defName = "") {
            return DxxUrl.GetFileName(uri);
        }

        public override void Download(DxxTargetInfo target, Action<bool> onCompleted = null) {
            StorageManager.Download(target, this, onCompleted);
        }


        private class Extractor : IDxxLinkExtractor {
            //WeakReference<IpondoDriver> mDriver;
            //IpondoDriver Driver => mDriver?.GetValue();

            public Extractor() {
            }

            public IEnumerable<Uri> ExtractLinks(string html) {
                throw new NotImplementedException();
            }

            public bool IsContainer(DxxUriEx url) {
                throw new NotImplementedException();
            }

            public bool IsContainerList(DxxUriEx url) {
                throw new NotImplementedException();
            }

            public bool IsTarget(DxxUriEx url) {
                throw new NotImplementedException();
            }

            public Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx url) {
                throw new NotImplementedException();
            }

            public Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx url) {
                throw new NotImplementedException();
            }
        }
        private class Storage : IDxxStorageManager {
            public void Download(DxxTargetInfo target, IDxxDriver driver, Action<bool> onCompleted = null) {
                DxxDBStorage.Instance.Download(target, driver, onCompleted);
            }

            public string GetSavedFile(Uri url) {
                return DxxDBStorage.Instance.GetSavedFile(url);
            }

            public bool IsDownloaded(Uri url) {
                return DxxDBStorage.Instance.IsDownloaded(url);
            }
        }
    }
}
