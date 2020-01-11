using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Common
{
    public static class Utils {
        public static T GetValue<T>(this WeakReference<T> w) where T : class {
            return w.TryGetTarget(out T o) ? o : null;
        }

        public static bool IsNullOrEmpty<T>(IEnumerable<T> v) {
            return !(v?.Any() ?? false);
        }

        public static DependencyObject FindChild(DependencyObject root, string name, Type type) {
            for(int i=0, ci=VisualTreeHelper.GetChildrenCount(root); i<ci; i++) {
                var ch = VisualTreeHelper.GetChild(root, i) as FrameworkElement;
                if(ch!=null && ch.GetType()==type && ch.Name == name) {
                    return ch;
                }
                var sub = FindChild(ch, name, type);
                if(null!=sub) {
                    return sub;
                }
            }
            return null;
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject {
            if (depObj != null) {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T) {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child)) {
                        yield return childOfChild;
                    }
                }
            }
        }

        public static T Apply<T>(this T obj, Action<T> fn) where T : class {
            fn(obj);
            return obj;
        }
        public static R Run<T,R>(this T obj, Func<T,R> fn) where T : class {
            return fn(obj);
        }
    }
}
