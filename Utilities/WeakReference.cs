using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utilities
{
    using System;
    using System.Runtime.InteropServices;

    public class WeakReference<T> : IDisposable        
    {
        private GCHandle handle;
        private bool trackResurrection;

        public WeakReference(T target)
            : this(target, false)
        {
        }

        public WeakReference(T target, bool trackResurrection)
        {
            this.trackResurrection = trackResurrection;
            this.Target = target;
        }

        ~WeakReference()
        {
            Dispose();
        }

        public void Dispose()
        {
            handle.Free();
            GC.SuppressFinalize(this);
        }

        public virtual bool IsAlive
        {
            get { return (handle.Target != null); }
        }

        public virtual bool TrackResurrection
        {
            get { return this.trackResurrection; }
        }

        public virtual T Target
        {
            get
            {
                object o = handle.Target;
                if ((o == null) || (!(o is T)))
                    return default(T);
                else
                    return (T)o;
            }
            set
            {
                handle = GCHandle.Alloc(value,
                  this.trackResurrection ? GCHandleType.WeakTrackResurrection : GCHandleType.Weak);
            }
        }

        public static implicit operator WeakReference<T>(T obj)  
        {
            return new WeakReference<T>(obj);
        }

        public static implicit operator T(WeakReference<T> weakRef)  
        {
            return weakRef.Target;
        }
    }
}
