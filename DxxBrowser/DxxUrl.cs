using DxxBrowser.driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxUrl {
        public Uri Uri { get; }
        public IDxxDriver Driver { get; }
        public string Description { get; }

        public DxxUrl(Uri uri, IDxxDriver driver, string description) {
            Uri = uri;
            Driver = driver;
            Description = description;
        }

        public DxxUrl(DxxTargetInfo info, IDxxDriver driver) {
            Uri = new Uri(info.Url);
            Driver = driver;
            Description = info.Description;
        }

        private bool mActualHasTargets = true;
        private bool mActualHasTargetContainers = true;

        public bool IsContainer => mActualHasTargets && Driver.LinkExtractor.IsContainer(Uri);

        public bool IsContainerList => mActualHasTargetContainers && Driver.LinkExtractor.IsContainerList(Uri);

        public bool IsTarget => Driver.LinkExtractor.IsTarget(Uri);

        public async Task<IList<DxxTargetInfo>> TryGetTargetContainers() {
            if(!IsContainerList) { 
                return null;
            }
            var r = await Driver.LinkExtractor.ExtractContainerList(Uri);
            if(r==null||r.Count==0) {
                mActualHasTargetContainers = false;
                return null;
            }
            return r;
        }

        public async Task<IList<DxxTargetInfo>> TryGetTargets() {
            if(!IsContainer) {
                return null;
            }
            var r = await Driver.LinkExtractor.ExtractTargets(Uri);
            if (r == null || r.Count == 0) {
                mActualHasTargets = false;
                return null;
            }
            return r;
        }

        public static async Task DownloadTargets(IDxxDriver driver, IList<DxxTargetInfo> targets) {
            if(targets==null||targets.Count==0) {
                return;
            }
            foreach(var t in targets) {
                var uri = new Uri(t.Url);
                if (driver.LinkExtractor.IsTarget(uri)) {
                    if(driver.StorageManager.IsDownloaded(uri)) {
                        DxxLogger.Instance.Info($"Skip (already downloaded): {GetFileName(uri)}");
                    } else if (DxxDownloader.Instance.IsDownloading(t.Url)) {
                        DxxLogger.Instance.Info($"Skip (already downloading): {GetFileName(uri)}");
                    } else {
                        DxxLogger.Instance.Info($"Start: {GetFileName(uri)}");
                        await driver.StorageManager.Download(uri, t.Description);
                    }
                } else { 
                    var du = new DxxUrl(t, driver);
                    var cnt = await du.TryGetTargetContainers();
                    if(cnt!=null && cnt.Count>0) {
                        DxxLogger.Instance.Info($"{cnt.Count} containers in {du.FileName}");
                        await DownloadTargets(driver, cnt);
                    }
                    var tgt = await du.TryGetTargets();
                    if(tgt!=null && tgt.Count>0) {
                        DxxLogger.Instance.Info($"{tgt.Count} targets in {du.FileName}");
                        await DownloadTargets(driver, tgt);
                    }
                }
            }
        }

        public async Task Download() {
            if(Driver.LinkExtractor.IsTarget(Uri)) {
                _ = Driver.StorageManager.Download(Uri, Description);
            }
            await DownloadTargets(Driver, await TryGetTargetContainers());
            await DownloadTargets(Driver, await TryGetTargets());
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
