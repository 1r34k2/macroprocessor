using System;
using System.Linq;

namespace SystemSoftware.Common
{
	public static class EnumerableExtensions
	{
		public static bool In<T>(this T entity, params T[] list)
		{
			return list.Contains(entity);
		}
	}

	public static class EnumHelper
	{
		/// <summary>
		/// Gets an attribute on an enum field value
		/// </summary>
		/// <typeparam name="T">The type of the attribute you want to retrieve</typeparam>
		/// <param name="enumVal">The enum value</param>
		/// <returns>The attribute of type T that exists on the enum value</returns>
		/// <example>string desc = myEnumVariable.GetAttributeOfType<DescriptionAttribute>().Description;</example>
		public static T GetAttributeOfType<T>(this Enum enumVal) where T : Attribute
		{
			var type = enumVal.GetType();
			var memInfo = type.GetMember(enumVal.ToString());
			var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
			return (attributes.Length > 0) ? (T)attributes[0] : null;
		}
	}

	/// <summary>
	/// Методы расширения класса string.
	/// </summary>
	public static class StringExtensions
	{
		/// <summary>
		/// Проверка на равенство строк без учета регистра.
		/// </summary>
		/// <param name="str1">Первая строка.</param>
		/// <param name="str2">Вторая строка.</param>
		/// <returns>Равны ли строки без учета регистра.</returns>
		public static bool EqualsIgnoreCase(this string str1, string str2)
		{
			return str1.Equals(str2, StringComparison.InvariantCultureIgnoreCase);
		}
	}
}
