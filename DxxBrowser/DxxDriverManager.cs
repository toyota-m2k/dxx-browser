using DxxBrowser.driver;
using DxxBrowser.driver.caribbean;
using DxxBrowser.driver.dmm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace DxxBrowser
{
    public class DxxDriverManager
    {
        public static DxxDriverManager Instance { get; } = new DxxDriverManager();

        List<IDxxDriver> mList;

        public static IDxxDriver NOP = new NopDriver();
        public static IDxxDriver DEFAULT = new DefaultDriver();


        private DxxDriverManager() {
            mList = new List<IDxxDriver>();
            mList.Add(new DmmDriver());
            mList.Add(new CaribbeanDriver());
        }

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

        //public DxxUrl FromUrl(string url) {
        //    var driver = FindDriver(url);
        //    if(null!=driver) {
        //        return new DxxUrl(new Uri(url), driver);
        //    } else {
        //        return null;
        //    }
        //}

        //public IDxxDriver CurrentDriver { get; private set; }

        //public void UpdateCurrentDriver(IDxxDriver driver) {
        //    if(null!=driver) {
        //        CurrentDriver = driver;
        //    }
        //}

        //public IDxxDriver UpdateCurrentDriverFor(string url) {
        //    UpdateCurrentDriver(FindDriver(url));
        //    return CurrentDriver;
        //}

        private const string SETTINGS_PATH = "DxxDriverSettings.xml";
        private const string ROOT_NAME = "DxxSettings";

        private XmlDocument getSettings() {
            try {
                var doc = new XmlDocument();
                doc.Load(SETTINGS_PATH);
                return doc;
            } catch(Exception) {
                var doc = new XmlDocument();
                doc.AppendChild(doc.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"utf-8\""));
                doc.AppendChild(doc.CreateElement(ROOT_NAME));
                return doc;
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
    }
}
