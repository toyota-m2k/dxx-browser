using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DxxBrowser.driver.dmm
{
    public class DmmDriver : IDxxDriver
    {
        private string StoragePath { get; set; }

        public string Name => "DMM";

        public string ID => "dmmFileBasedDriver";

        public IDxxLinkExtractor LinkExtractor { get; private set; }

        public IDxxStorageManager StorageManager { get; private set; }

        private const string KEY_STORAGE_PATH = "StoragePath";

        public bool LoadSettins(XmlElement settings) {
            StoragePath = settings.GetAttribute(KEY_STORAGE_PATH);
            return true;
        }

        public bool SaveSettings(XmlElement settings) {
            settings.SetAttribute(KEY_STORAGE_PATH, StoragePath);
            return true;
        }

        public bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("dmm.com");
        }

        public bool Setup() {
            var dlg = new DmmSettingsDialog(StoragePath);
            if(dlg.ShowDialog() ?? false) {
                StoragePath = dlg.Path;
                return true;
            }
            return false;
            
        }

        public DmmDriver() {
            LinkExtractor = new Extractor(this);
            StorageManager = new Storage(this);
        }

        class Storage : IDxxStorageManager {
            WeakReference<DmmDriver> mDriver;
            DmmDriver Driver => mDriver?.GetValue();

            public Storage(DmmDriver driver) {
                mDriver = new WeakReference<DmmDriver>(driver);
            }

            public Task<bool> Download(string url) {
                throw new NotImplementedException();
            }

            public Task<string> GetSavedFile(string url) {
                throw new NotImplementedException();
            }

            public bool IsDownloaded(string url) {
                throw new NotImplementedException();
            }
        }

        class Extractor : IDxxLinkExtractor {
            WeakReference<DmmDriver> mDriver;
            DmmDriver Driver => mDriver?.GetValue();

            public Extractor(DmmDriver driver) {
                mDriver = new WeakReference<DmmDriver>(driver);
            }

            public Task<IList<string>> ExtractTargetContainers(string url) {
                throw new NotImplementedException();
            }

            public Task<IList<string>> ExtractTargets(string url) {
                throw new NotImplementedException();
            }

            public bool HasTargetContainers(string url) {
                throw new NotImplementedException();
            }

            public bool HasTargets(string url) {
                throw new NotImplementedException();
            }
        }
    }
}
