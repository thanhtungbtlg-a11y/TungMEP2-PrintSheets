using System.Collections.Generic;

namespace TungMEP2.PrintSheets.Execution;

public static class NativeObjectIdentity
{
	public static bool IsSame<T>(T expected, T active) where T : class =>
		expected != null &&
		active != null &&
		EqualityComparer<T>.Default.Equals(expected, active);
}
