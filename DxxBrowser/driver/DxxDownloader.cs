using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    public class DxxDownloadingItem : DxxViewModelBase {
        const string STATUS_BEGIN = "Downloading ...";
        const string STATUS_ERROR = "NG: error.";
        const string STATUS_COMPLETED = "OK: downloaded.";

        public enum DownloadStatus {
            Downloading,
            Completed,
            Error,
        }

        private DownloadStatus mStatus;

        public string Url { get; }
        public string Description { get; }
        public string Name => DxxUrl.GetFileName(Url);

        public DownloadStatus Status {
            get => mStatus;
            set => setProp(callerName(), ref mStatus, value, "StatusString");
        }

        public string StatusString {
            get {
                switch(Status) {
                    default:
                    case DownloadStatus.Downloading:
                        return STATUS_BEGIN;
                    case DownloadStatus.Completed:
                        return STATUS_COMPLETED;
                    case DownloadStatus.Error:
                        return STATUS_ERROR;
                }
            }
        }

        public DxxDownloadingItem(string url, string description) {
            Url = url;
            Description = description;
            Status = DownloadStatus.Downloading;
        }
    }

    public class DxxDownloader {
        HttpClient mHttpClient = new HttpClient();
        HashSet<string> mDownloading = new HashSet<string>();
        Dictionary<string, CancellationTokenSource> mCancellationTokens = new Dictionary<string, CancellationTokenSource>();
        bool mTerminated = false;
        public ObservableCollection<DxxDownloadingItem> DownloadingStateList { get; } = new ObservableCollection<DxxDownloadingItem>();


        public Subject<bool> Busy = new Subject<bool>();

        event Action AllDownloadCompleted;

        public static DxxDownloader Instance { get; } = new DxxDownloader();

        private DxxDownloader() {
        }

        private CancellationTokenSource LockUrl(Uri uri, string description) {
            lock (mDownloading) {
                if(mTerminated) {
                    return null;
                }
                var url = uri.ToString();
                if (mDownloading.Contains(url)) {
                    return null;
                }
                DownloadingStateList.Add(new DxxDownloadingItem(url, description));
                mDownloading.Add(url);
                var cts = new CancellationTokenSource();
                mCancellationTokens.Add(url, cts);
                Busy.OnNext(true);
                return cts;
            }
        }

        private void UnlockUrl(Uri uri, bool completed) {
            lock (mDownloading) {
                var url = uri.ToString();
                mDownloading.Remove(url);
                mCancellationTokens.Remove(url);
                var t = DownloadingStateList.Where((v) => v.Url == url).Select((v) => {
                    v.Status = completed ? DxxDownloadingItem.DownloadStatus.Completed : DxxDownloadingItem.DownloadStatus.Error;
                    return v;
                });
                if (mDownloading.Count==0) {
                    AllDownloadCompleted?.Invoke();
                    Busy.OnNext(false);
                }
            }
        }

        public bool IsBusy {
            get {
                lock (mDownloading) {
                    return mDownloading.Count > 0;
                }
            }
        }

        public bool IsDownloading(string url) {
            lock (mDownloading) {
                return mDownloading.Contains(url);
            }
        }

        /**
         * アプリ終了時のダウンロード完了待ち
         * @param forceShutdown trueで呼ぶと、通信中のタスクをすべてキャンセルする。
         *                      false なら、通信中のタスクが完了するまで待つ。
         */
        public async Task TerminateAsync(bool forceShutdown) {
            var tc = new TaskCompletionSource<object>();
            Action completed = () => {
                tc.TrySetResult(null);
            };

            lock (mDownloading) {
                mTerminated = true;
                if (mDownloading.Count == 0 || AllDownloadCompleted != null) {
                    return;
                }
                AllDownloadCompleted += completed;
                if (forceShutdown) {
                    foreach (var cts in mCancellationTokens) {
                        DxxLogger.Instance.Warn($"Cancelling: {DxxUrl.GetFileName(cts.Key)}");
                        cts.Value.Cancel();
                    }
                }
            }
            await tc.Task;
            AllDownloadCompleted -= completed;
        }

        public async Task CancelAllAsync() {
            var tc = new TaskCompletionSource<object>();
            Action completed = () => {
                tc.TrySetResult(null);
            };
            lock (mDownloading) {
                if (mDownloading.Count == 0 || AllDownloadCompleted != null) {
                    return;
                }
                AllDownloadCompleted += completed;
                foreach (var cts in mCancellationTokens) {
                    DxxLogger.Instance.Warn($"Cancelling: {DxxUrl.GetFileName(cts.Key)}");
                    cts.Value.Cancel();
                }
            }
            await tc.Task;
            AllDownloadCompleted -= completed;
        }

        public void Cancel(string url) {
            if(string.IsNullOrEmpty(url)) {
                return;
            }
            lock (mDownloading) {
                if (mCancellationTokens.TryGetValue(url, out var cts)) {
                    if (null != cts) {
                        DxxLogger.Instance.Warn($"Cancelling: {DxxUrl.GetFileName(url)}");
                        cts.Cancel();
                    }
                }
            }
        }

        public async Task<bool> DownloadAsync(Uri uri, string filePath, string description) {
            var cts = LockUrl(uri, description);
            if(null==cts) {
                return false;
            }
            bool result = false;
            try {
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var response = await mHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)) {
                    if (response.StatusCode == HttpStatusCode.OK) {
                        using (var content = response.Content)
                        using (var stream = await content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                            await stream.CopyToAsync(fileStream);
                            result = true;
                            DxxLogger.Instance.Info($"Completed: {DxxUrl.GetFileName(uri)}");
                        }
                    }
                }
            } catch (Exception) {
            } finally {
                if(!result) {
                    DxxLogger.Instance.Error($"Error: {DxxUrl.GetFileName(uri)}");
                    if (File.Exists(filePath)) {
                        File.Delete(filePath);
                    }
                }
                UnlockUrl(uri, result);
            }
            return result;
        }
    }
}
