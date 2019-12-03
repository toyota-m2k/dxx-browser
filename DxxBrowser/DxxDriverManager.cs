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

        public void InitializeDrivers() {
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

        public IDxxDriver CurrentDriver { get; private set; }

        public void UpdateCurrentDriver(IDxxDriver driver) {
            if(null!=driver) {
                CurrentDriver = driver;
            }
        }

        public IDxxDriver UpdateCurrentDriverFor(string url) {
            UpdateCurrentDriver(FindDriver(url));
            return CurrentDriver;
        }

        private const string SETTINGS_PATH = "DxxDriverSettings.xml";

        private XmlDocument getSettings() {
            try {
                var doc = new XmlDocument();
                doc.Load(SETTINGS_PATH);
                return doc;
            } catch(Exception e) {
                return new XmlDocument();
            }
        }

        public void LoadSettings() {
            var doc = getSettings();
            foreach(var d in mList) {
                var elems = doc.GetElementsByTagName(d.ID);
                if(elems.Count>0) {
                }
            }
        }

    }
}
