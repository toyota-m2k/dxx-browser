using DxxBrowser.common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace DxxBrowser.driver {
    public abstract class DxxDriverBaseStoragePathSupport : IDxxDriver {
        public string StoragePath { get; private set; }

        private const string KEY_STORAGE_PATH = "StoragePath";
        public bool LoadSettins(XmlElement settings) {
            StoragePath = settings.GetAttribute(KEY_STORAGE_PATH);
            return true;
        }

        public bool SaveSettings(XmlElement settings) {
            settings.SetAttribute(KEY_STORAGE_PATH, StoragePath);
            return true;
        }

        public bool Setup(XmlElement settings, Window owner) {
            var dlg = new DxxStorageFolderDialog(Name, StoragePath);
            dlg.Owner = owner;
            if (dlg.ShowDialog() ?? false) {
                if(IsSameDirectoryPath(dlg.Path, StoragePath)) {
                    return false;
                }
                StoragePath = dlg.Path;
                using (MicWaitCursor.Start(owner)) {
                    DxxDBStorage.Instance.MoveStorageFolder(Name, StoragePath);
                    if (null != settings) {
                        SaveSettings(settings);
                    }
                }
                return true;
            }
            return false;
        }

        public static bool IsSameDirectoryPath(string p1, string p2) {
            if (!p1.EndsWith(@"\")) {
                p1 = p1 + @"\";
            }
            if (!p2.EndsWith(@"\")) {
                p2 = p2 + @"\";
            }
            return p1.ToLower() == p2.ToLower();
        }

        public bool HasSettings => true;


        public abstract string Name { get; }
        public abstract string ID { get; }
        public abstract IDxxLinkExtractor LinkExtractor { get; }
        public abstract IDxxStorageManager StorageManager { get; }

        public abstract void Download(DxxTargetInfo target, Action<bool> onCompleted = null);
        public abstract string GetNameFromUri(Uri uri, string defName = "");
        public abstract bool IsSupported(string url);
        public abstract string ReserveFilePath(Uri uri);
    }
}
