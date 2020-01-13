using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace DxxBrowser
{
    public interface IDxxDriver
    {
        string Name { get; }
        string ID { get; }
        bool HasSettings { get; }
        bool Setup(XmlElement settings, Window owner);
        bool LoadSettins(XmlElement settings);
        bool SaveSettings(XmlElement settings);

        string StoragePath { get; }
        /**
         * 保存ファイルのパスを返す。
         * DBStorageに任せるときは、nullを返す。
         */
        string ReserveFilePath(Uri uri);

        bool IsSupported(string url);
        string GetNameFromUri(Uri uri, string defName="");
        void Download(DxxTargetInfo target, Action<bool> onCompleted=null);

        IDxxLinkExtractor LinkExtractor { get; }
        IDxxStorageManager StorageManager { get; }
    }
}
