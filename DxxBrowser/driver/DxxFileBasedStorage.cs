using Common;
using System;
using System.IO;

namespace DxxBrowser.driver {
    public class DxxFileBasedStorage : IDxxStorageManager {
        WeakReference<IDxxDriver> mDriver;
        protected IDxxDriver Driver => mDriver?.GetValue();

        public DxxFileBasedStorage(IDxxDriver driver) {
            mDriver = new WeakReference<IDxxDriver>(driver);
        }

        protected virtual string GetPath(Uri uri) {
            var filename = DxxUrl.GetFileName(uri);
            return Path.Combine(Driver.StoragePath, filename);
        }

        protected virtual string LOG_CAT => "FileStorage";

        public void Download(DxxTargetInfo target, IDxxDriver driver, Action<bool> onCompleted) {
            var path = GetPath(target.Uri);
            if (File.Exists(path)) {
                DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped (register db): {target.Name} {target.Description}");
                DxxDBStorage.Instance.RegisterAsCompleted(target, path, Driver.Name);
                DxxPlayer.PlayList.AddSource(DxxPlayItem.FromTarget(target));
                onCompleted?.Invoke(false);
                return;
            }

            if (!Driver.LinkExtractor.IsTarget(target)) {
                onCompleted?.Invoke(false);
                return;
            }

            // ダウンロードは DB Storage に任せる
            DxxDBStorage.Instance.Download(target, driver, onCompleted);

#if false
            if (DxxNGList.Instance.IsNG(target.Url)) {
                DxxLogger.Instance.Cancel(LOG_CAT, $"Dislike ({target.Name})");
                onCompleted?.Invoke(false);
                return;
            }
            if(DxxDBStorage.Instance.IsDownloaded(target.Uri)) {
                DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped (already downloaded): {target.Name} {target.Description}");
                DxxPlayer.PlayList.AddSource(DxxPlayItem.FromTarget(target));
                onCompleted?.Invoke(false);
                return;
            }

            if(DxxDownloader.Instance.IsDownloading(target.Url)) {
                DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped (already downloading): {target.Name}");
                onCompleted?.Invoke(false);
                return;
            }
            DxxLogger.Instance.Comment(LOG_CAT, $"Start: {target.Name}");
            DxxDownloader.Instance.Reserve(target, path, DxxDownloader.MAX_RETRY, (v)=> {
                if(v) {
                    DxxPlayer.PlayList.AddSource(DxxPlayItem.FromTarget(target));
                    DxxLogger.Instance.Success(LOG_CAT, $"Completed: {target.Name}");
                } else {
                    DxxLogger.Instance.Error(LOG_CAT, $"Error: {target.Name}");
                }
                onCompleted?.Invoke(v);
            });
#endif
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
