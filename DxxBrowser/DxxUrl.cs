﻿using DxxBrowser.driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxUrl : DxxTargetInfo {
        public IDxxDriver Driver { get; }

        //public DxxUrl(string url, IDxxDriver driver, string name, string description) : base(url, name, description) {
        //    Driver = driver;
        //}
        public DxxUrl(Uri uri, IDxxDriver driver, string name, string description) : base(uri, name, description) {
            Driver = driver;
        }

        public DxxUrl(DxxTargetInfo info, IDxxDriver driver) : base(info.Url, info.Name, info.Description) {
            Driver = driver;
        }

        private bool mActualHasTargets = true;
        private bool mActualHasTargetContainers = true;

        public bool IsContainer => mActualHasTargets && Driver.LinkExtractor.IsContainer(this);

        public bool IsContainerList => mActualHasTargetContainers && Driver.LinkExtractor.IsContainerList(this);
        public bool IsTarget => Driver.LinkExtractor.IsTarget(this);

        public async Task<IList<DxxTargetInfo>> TryGetTargetContainers() {
            if(!IsContainerList) { 
                return null;
            }
            var r = await Driver.LinkExtractor.ExtractContainerList(this);
            if(r==null||r.Count==0) {
#if !DEBUG
                mActualHasTargetContainers = false;
#endif
                return null;
            }
            return r;
        }

        public async Task<IList<DxxTargetInfo>> TryGetTargets() {
            if(!IsContainer) {
                return null;
            }
            var r = await Driver.LinkExtractor.ExtractTargets(this);
            if (r == null || r.Count == 0) {
#if !DEBUG
                mActualHasTargets = false;
#endif
                return null;
            }
            return r;
        }

        private const string LOG_CAT = "URL";

        public static async void DownloadTargets(IDxxDriver driver, IList<DxxTargetInfo> targets) {
            if(targets==null||targets.Count==0) {
                return;
            }
            await DxxActivityWatcher.Instance.Execute<object>(async (cancellationToken) => {
                try {
                    foreach (var t in targets) {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (driver.LinkExtractor.IsTarget(t)) {
                            if (driver.StorageManager.IsDownloaded(t.Uri)) {
                                DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped (already downloaded): {t.Name}");
                            } else if (DxxDownloader.Instance.IsDownloading(t.Url)) {
                                DxxLogger.Instance.Cancel(LOG_CAT, $"Skipped (already downloading): {t.Name}");
                            } else {
                                DxxLogger.Instance.Comment(LOG_CAT, $"Start: {t.Name}");
                                driver.StorageManager.Download(t);
                            }
                        } else {
                            var du = new DxxUrl(t, driver);
                            var cnt = await du.TryGetTargetContainers();
                            if (cnt != null && cnt.Count > 0) {
                                DxxLogger.Instance.Comment(LOG_CAT, $"{cnt.Count} containers in {du.FileName}");
                                DownloadTargets(driver, cnt);
                            }
                            var tgt = await du.TryGetTargets();
                            if (tgt != null && tgt.Count > 0) {
                                DxxLogger.Instance.Comment(LOG_CAT, $"{tgt.Count} targets in {du.FileName}");
                                DownloadTargets(driver, tgt);
                            }
                        }
                    }
                } catch (Exception e) {
                    if (e is OperationCanceledException) {
                        DxxLogger.Instance.Cancel(LOG_CAT, "Download Cancelled.");
                    } else {
                        DxxLogger.Instance.Error(LOG_CAT, $"Download Error: {e.Message}");
                    }
                }
                return null;
            }, null);
            
        }

        public async Task<bool> Download() {
            bool result = false;
            if(Driver.LinkExtractor.IsTarget(this)) {
                Driver.StorageManager.Download(new DxxTargetInfo(Url, Name, Description));
                result = true;
            }
            var containers = await TryGetTargetContainers();
            if (!Utils.IsNullOrEmpty(containers)) {
                DownloadTargets(Driver, containers);
                result = true;
            }
            var targets = await TryGetTargets();
            if (!Utils.IsNullOrEmpty(targets)) {
                DownloadTargets(Driver, targets);
                result = true;
            }
            return result;
        }

        public string Host => Uri.Host;
        public string FileName => GetFileName(Uri);
        public string URL => Uri.ToString();

        public static string GetFileName(Uri uri) {
            return uri.Segments.Last();
        }

        public static string GetFileName(string url) {
            var uri = new Uri(url);
            return GetFileName(uri);
        }

        public static Uri FixUpUrl(string url) {
            if (string.IsNullOrEmpty(url)) {
                return null;
            }
            try {
                return new Uri(url);
            } catch(Exception) {
                if(url.StartsWith("//")) {
                    return FixUpUrl("http:" + url);
                } else if(!url.StartsWith("http")) {
                    return FixUpUrl("http://" + url);
                } else {
                    return null;
                }
            }
        }
    }
}
