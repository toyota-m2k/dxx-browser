using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser
{
    public static class Utils {
        public static T GetValue<T>(this WeakReference<T> w) where T : class {
            return w.TryGetTarget(out T o) ? o : null;
        }
    }
}
