using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxGlobal {

        public WinPlacement Placement { get; set; } = new WinPlacement();
        public DxxDBViewerWindow.SortInfo SortInfo { get; set; } = new DxxDBViewerWindow.SortInfo();



        private const string SETTINGS_FILE = "DxxSettings.xml";

        private static DxxGlobal sInstance = null;
        public static DxxGlobal Instance {
            get {
                if (sInstance == null) {
                    sInstance = Deserialize();
                }
                return sInstance;
            }
        }

        public void Serialize() {
            System.IO.StreamWriter sw = null;
            try {
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(DxxGlobal));
                //書き込むファイルを開く（UTF-8 BOM無し）
                sw = new System.IO.StreamWriter(SETTINGS_FILE, false, new System.Text.UTF8Encoding(false));
                //シリアル化し、XMLファイルに保存する
                serializer.Serialize(sw, this);
            } catch (Exception e) {
                Debug.WriteLine(e);
            } finally {
                //ファイルを閉じる
                if (null != sw) {
                    sw.Close();
                }
            }
        }

        public static DxxGlobal Deserialize() {
            System.IO.StreamReader sr = null;
            Object obj = null;

            try {
                //XmlSerializerオブジェクトを作成
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(DxxGlobal));

                //読み込むファイルを開く
                sr = new System.IO.StreamReader(SETTINGS_FILE, new System.Text.UTF8Encoding(false));

                //XMLファイルから読み込み、逆シリアル化する
                obj = serializer.Deserialize(sr);
            } catch (Exception e) {
                Debug.WriteLine(e);
                obj = new DxxGlobal();
            } finally {
                if (null != sr) {
                    //ファイルを閉じる
                    sr.Close();
                }
            }
            return (DxxGlobal)obj;
        }
    }
}
