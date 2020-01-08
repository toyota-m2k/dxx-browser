using Common;
using DxxBrowser.driver;
using DxxBrowser.driver.caribbean;
using DxxBrowser.driver.dmm;
using DxxBrowser.driver.heizo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Xml;

namespace DxxBrowser {
    /**
     * ダウンロードドライバーを統べる者
     */
    public class DxxDriverManager : IDisposable
    {
        public static DxxDriverManager Instance { get; private set; }
        public static IDxxDriver NOP = new NopDriver();
        public static IDxxDriver DEFAULT = new DefaultDriver();

        private List<IDxxDriver> mList;

        /**
         * コンストラクタ
         */
        private DxxDriverManager() {
            mList = new List<IDxxDriver>();
            mList.Add(new DmmDriver());
            mList.Add(new CaribbeanDriver());
            mList.Add(new HeyzoDriver());
        }

        public static void Initialize(Window owner) {
            DxxDBStorage.Initialize();
            Instance = new DxxDriverManager();
            Instance.LoadSettings(owner);
        }

        public static void Terminate() {
            Instance.Dispose();
        }

        /**
         * URLを処理できるドライバーを探す
         */
        public IDxxDriver FindDriver(string url) {
            try {
                if(string.IsNullOrEmpty(url)||!url.StartsWith("http")) {
                    return null;
                }
                var drv = mList.Where((v) => v.IsSupported(url));
                if(!Utils.IsNullOrEmpty(drv)) {
                    return drv.First();
                } else {
                    return DEFAULT;
                }
            } catch(Exception) {
                return null;
            }
        }

        public void LoadSettings(Window owner) {
            var doc = getSettings();
            var root = doc.GetElementsByTagName(ROOT_NAME)[0];
            bool update = false;

            var driverList = (new IDxxDriver[] { DEFAULT }).Concat(mList);
            foreach(var d in driverList) {
                var elems = doc.GetElementsByTagName(d.ID);
                if(elems.Count>0) {
                    var el = elems[0];
                    d.LoadSettins(el as XmlElement);
                } else {
                    var el = doc.CreateElement(d.ID);
                    if (d.Setup(el,owner)) {
                        root.AppendChild(el);
                        update = true;
                    }
                }
            }
            if(update) {
                doc.Save(SETTINGS_PATH);
            }
        }

        public void Setup(IDxxDriver targetDriver, Window owner) {
            if(!targetDriver.HasSettings) {
                return;
            }
            var doc = getSettings();
            var root = doc.GetElementsByTagName(ROOT_NAME)[0];
            var el = root.SelectSingleNode(targetDriver.ID);
            if(el==null) {
                el = doc.CreateElement(targetDriver.ID);
                root.AppendChild(el);
            }
            if(targetDriver.Setup(el as XmlElement, owner)) {
                doc.Save(SETTINGS_PATH);
            }
        }

        private const string SETTINGS_PATH = "DxxDriverSettings.xml";
        private const string ROOT_NAME = "DxxSettings";

        private XmlDocument getSettings() {
            try {
                var doc = new XmlDocument();
                doc.Load(SETTINGS_PATH);
                return doc;
            } catch (Exception) {
                var doc = new XmlDocument();
                doc.AppendChild(doc.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"utf-8\""));
                doc.AppendChild(doc.CreateElement(ROOT_NAME));
                return doc;
            }
        }

        public void Dispose() {
            DxxDBStorage.Terminate();
        }
    }
}
