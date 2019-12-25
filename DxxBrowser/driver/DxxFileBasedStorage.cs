using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    public interface IDxxFileBasedDriver : IDxxDriver {
        string StoragePath { get; }
    }

    public class DxxFileBasedStorage : IDxxStorageManager {
        WeakReference<IDxxFileBasedDriver> mDriver;
        protected IDxxFileBasedDriver Driver => mDriver?.GetValue();

        public DxxFileBasedStorage(IDxxFileBasedDriver driver) {
            mDriver = new WeakReference<IDxxFileBasedDriver>(driver);
        }

        protected virtual string GetPath(Uri uri) {
            var filename = DxxUrl.GetFileName(uri);
            return Path.Combine(Driver.StoragePath, filename);
        }

        public void Download(DxxTargetInfo target, Action<bool> onCompleted) {
            if (!Driver.LinkExtractor.IsTarget(target)) {
                onCompleted?.Invoke(false);
                return;
            }
            var path = GetPath(target.Uri);
            if (File.Exists(path)) {
                onCompleted?.Invoke(false);
                return;
            }
            DxxDownloader.Instance.Download(target, path, (v)=> {
                if(v) {
                    DxxPlayer.GetInstance().AddSource(new Uri(path));
                }
            });
        }

        public string GetSavedFile(Uri uri) {
            var path = GetPath(uri);
            return File.Exists(path) ? path : null;
        }

        public bool IsDownloaded(Uri uri) {
            var path = GetPath(uri);
            return File.Exists(path);
        }
    }
}
