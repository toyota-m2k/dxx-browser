using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Common {
    /**
     * IDisposable なプロパティをDisposeするかどうかを指定するためのアノテーションクラス
     * 
     * 使用例は、DxxViewModelBase#Dispose()を参照
     */
    public class Disposal : System.Attribute {
        public bool ToBeDisposed { get; }
        public Disposal(bool disposable = true) {
            ToBeDisposed = disposable;
        }
    }

    public class MicPropertyChangeNotifier : INotifyPropertyChanged {
        #region INotifyPropertyChanged i/f
        //-----------------------------------------------------------------------------------------

        public event PropertyChangedEventHandler PropertyChanged;
        protected void notify(string propName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        protected string callerName([CallerMemberName] string memberName = "") {
            return memberName;
        }

        protected bool setProp<T>(string name, ref T field, T value, params string[] familyProperties) {
            if (field != null ? !field.Equals(value) : value != null) {
                field = value;
                notify(name);
                foreach (var p in familyProperties) {
                    notify(p);
                }
                return true;
            }
            return false;
        }

        #endregion
    }

    public class MicViewModelBase : MicPropertyChangeNotifier, INotifyPropertyChanged, IDisposable {

        /**
         * Disposable な プロパティをすべてDisposeする。
         * ここでDisposeしては困るプロパティには、[Disposal(false)] を指定すること。
         */
        public virtual void Dispose() {
            var type = this.GetType();
            var props = type.GetProperties();
            foreach (var prop in props) {
                var obj = prop.GetValue(this);
                if (obj is IDisposable) {
                    var attrs = prop.GetCustomAttributes(false).Where((v) => v is Disposal);
                    if (((Disposal)attrs.FirstOrDefault())?.ToBeDisposed ?? true) {
                        ((IDisposable)obj).Dispose();
                    }
                }
            }
        }
    }

    public class MicViewModelBase<T> : MicViewModelBase, INotifyPropertyChanged, IDisposable where T : class {
        private WeakReference<T> mOwner;
        public T Owner {
            get => mOwner?.GetValue();
            set => mOwner = ( value == null ) ? null : new WeakReference<T>(value);
        }

        public MicViewModelBase(T owner=null ) {
            Owner = owner;
        }
    }
}