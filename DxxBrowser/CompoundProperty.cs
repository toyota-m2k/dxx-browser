using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class CompoundObservable<T> : IObservable<T>, IDisposable {
        public delegate T GetValueProc();
        private IEnumerable<WeakReference<INotifyPropertyChanged>> Dependencies;
        private GetValueProc ValueProc;
        private Subject<T> InternalSubject = new Subject<T>();

        public CompoundObservable(GetValueProc proc, params INotifyPropertyChanged[] args) {
            ValueProc = proc;
            if (args != null && args.Any()) {
                Dependencies = args.Select((v) => {
                    v.PropertyChanged += OnSourceChanged;
                    return new WeakReference<INotifyPropertyChanged>(v);
                });
                Fire();
            } else {
                Dependencies = new WeakReference<INotifyPropertyChanged>[0];
            }
        }

        public void AddSource(INotifyPropertyChanged obj) {
            obj.PropertyChanged += OnSourceChanged;
            Dependencies = Dependencies.Concat(new WeakReference<INotifyPropertyChanged>[] { new WeakReference<INotifyPropertyChanged>(obj) });
            Fire();
        }

        public void RemoveSource(INotifyPropertyChanged obj) {
            obj.PropertyChanged -= OnSourceChanged;
            Dependencies = Dependencies.Where((v) => v != null && v.TryGetTarget(out var p) && p != obj);
            InternalSubject.OnNext(ValueProc());
            Fire();
        }

        private void Fire() {
            if (null != ValueProc) {
                InternalSubject.OnNext(ValueProc());
            }
        }

        private void OnSourceChanged(object sender, PropertyChangedEventArgs e) {
            Fire();
        }

        public IDisposable Subscribe(IObserver<T> observer) {
            InternalSubject.Subscribe(observer);
            return this;
        }

        public void Dispose() {
            foreach(var v in Dependencies) {
                if(v!=null&&v.TryGetTarget(out var p) && p!=null) {
                    p.PropertyChanged -= OnSourceChanged;
                }
            }
            Dependencies = null;
        }
    }
}
