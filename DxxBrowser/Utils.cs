using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DxxBrowser
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
    }
}
