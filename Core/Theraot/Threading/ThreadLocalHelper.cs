using System;
using System.Threading;
using Theraot.Core;
using Theraot.Threading.Needles;

namespace Theraot.Threading
{
    internal static class ThreadLocalHelper
    {
        private static readonly Exception _recursionGuardException = new InvalidOperationException("Recursion");

        public static Exception RecursionGuardException
        {
            get
            {
                return _recursionGuardException;
            }
        }
    }

    internal static class ThreadLocalHelper<T>
    {
        private static readonly INeedle<T> _recursionGuardNeedle = new ExceptionStructNeedle<T>(ThreadLocalHelper.RecursionGuardException);

        public static INeedle<T> RecursionGuardNeedle
        {
            get
            {
                return _recursionGuardNeedle;
            }
        }
    }
}