#if VNEXT
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace winswTests.Util
{
	internal static class AsyncAssert
    {
        internal static async Task<TActual> ThrowsAsync<TActual>(AsyncTestDelegate code)
			where TActual : Exception
        {
			Exception caught = null;
			try
			{
				await code();
			}
			catch (Exception e)
			{
				caught = e;
			}

			Assert.That(caught, new ExceptionTypeConstraint(typeof(TActual)));
			return (TActual)caught;
        }
    }
}
#endif
