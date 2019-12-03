using DxxBrowser.driver.dmm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DxxBrowser
{
    public class DxxDriverManager
    {
        List<IDxxDriver> mList;

        public DxxDriverManager() {
            mList = new List<IDxxDriver>();
            mList.Add(new DmmDriver());
            LoadSettings();
        }

        public IDxxDriver FindDriver(string url) {
            try {
                return mList.Where((v) => v.IsSupported(url))
                            .First();
            } catch(Exception) {
                return null;
            }
        }

        public DxxUrl FromUrl(string url) {
            var driver = FindDriver(url);
            if(null!=driver) {
                var uri = new Uri(url);
                if(driver.LinkExtractor.HasTargets(uri)) {
                    return new DxxUrl(uri, DxxUrl.TargetType.Target, driver);
                } else if(driver.Link)

            }
        }

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

        private XmlDocument getSettings() {
            try {
                var doc = new XmlDocument();
                doc.Load(SETTINGS_PATH);
                return doc;
            } catch(Exception) {
                return new XmlDocument();
            }
        }

        public void LoadSettings() {
            var doc = getSettings();
            bool update = false;
            foreach(var d in mList) {
                var elems = doc.GetElementsByTagName(d.ID);
                if(elems.Count>0) {
                    var el = elems[0];
                    d.LoadSettins(el as XmlElement);
                } else {
                    var el = doc.CreateElement(d.ID);
                    d.Setup(el);
                    doc.AppendChild(el);
                    update = true;
                }
            }
            if(update) {
                doc.Save(SETTINGS_PATH);
            }
        }

    }
}
