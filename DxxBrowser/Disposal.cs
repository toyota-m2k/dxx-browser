using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    /**
     * IDisposable なプロパティをDisposeするかどうかを指定するためのアノテーションクラス
     * 
     * 使用例は、DxxViewModelBase#Dispose()を参照
     */
    public class Disposal : System.Attribute {
        public bool ToBeDisposed { get; }
        public Disposal(bool disposable=true) {
            ToBeDisposed = disposable;
        }
    }
}
