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

        protected virtual string LOG_CAT => "FileStorage";

        public void Download(DxxTargetInfo target, Action<bool> onCompleted) {
            if (!Driver.LinkExtractor.IsTarget(target)) {
                onCompleted?.Invoke(false);
                return;
            }
            if (DxxNGList.Instance.IsNG(target.Url)) {
                DxxLogger.Instance.Cancel(LOG_CAT, $"Dislike ({target.Name})");
                onCompleted?.Invoke(false);
                return;
            }
            var path = GetPath(target.Uri);
            if (File.Exists(path)) {
                DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped (already downloaded): {target.Name}");
                DxxPlayer.PlayList.AddSource(new DxxPlayItem(target.Uri));
                onCompleted?.Invoke(false);
                return;
            }
            if(DxxDownloader.Instance.IsDownloading(target.Url)) {
                DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped (already downloading): {target.Name}");
                onCompleted?.Invoke(false);
                return;
            }
            DxxLogger.Instance.Comment(LOG_CAT, $"Start: {target.Name}");
            DxxDownloader.Instance.Download(target, path, (v)=> {
                if(v) {
                    DxxPlayer.PlayList.AddSource(new DxxPlayItem(target.Uri));
                    DxxLogger.Instance.Success(LOG_CAT, $"Completed: {target.Name}");
                } else {
                    DxxLogger.Instance.Error(LOG_CAT, $"Error: {target.Name}");
                }
                onCompleted?.Invoke(v);
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
