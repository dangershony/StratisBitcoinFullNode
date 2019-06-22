using System;
using System.Runtime.CompilerServices;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure
{
    public static class Guard
    {
        public static void NotNull(object[] args, [CallerMemberName] string method = "")
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == null)
                {
                    throw new ArgumentNullException($"Parameter at index {i} of method {method}");
                }
            }
        }

        public static void NotNull(object arg, [CallerMemberName] string method = null)
        {
            if (arg == null)
                throw new ArgumentNullException($"Parameter of method {method}");
        }

        public static void NotNull(object arg, object arg1, [CallerMemberName] string method = "")
        {
            if (arg == null)
                throw new ArgumentNullException($"Parameter at index 0 of method {method}");
            if (arg1 == null)
                throw new ArgumentNullException($"Parameter at index 1 of method {method}");
        }

        

        public static void NotNull(object arg, object arg1,object arg2, [CallerMemberName] string method = "")
        {
            if (arg == null)
                throw new ArgumentNullException($"Parameter at index 0 of method {method}");
            if (arg1 == null)
                throw new ArgumentNullException($"Parameter at index 1 of method {method}");
            if (arg2 == null)
                throw new ArgumentNullException($"Parameter at index 2 of method {method}");
        }

        public static void NotNull(object arg, object arg1, object arg2, object arg3, [CallerMemberName] string method = "")
        {
            if (arg == null)
                throw new ArgumentNullException($"Parameter at index 0 of method {method}");
            if (arg1 == null)
                throw new ArgumentNullException($"Parameter at index 1 of method {method}");
            if (arg2 == null)
                throw new ArgumentNullException($"Parameter at index 2 of method {method}");
            if (arg3 == null)
                throw new ArgumentNullException($"Parameter at index 3 of method {method}");
        }
    }
}
