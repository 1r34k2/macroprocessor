namespace SystemSoftware.MacroProcessor
{
	/// <summary>
	/// Параметр макроса.
	/// </summary>
	public class MacroParameter
	{
		/// <summary>
		/// Название.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Тип.
		/// </summary>
		public MacroParameterTypes Type { get; set; }

		/// <summary>
		/// Значение по умолчанию.
		/// </summary>
		public int? DefaultValue { get; set; }

		public MacroParameter() { }

		public MacroParameter(string name, MacroParameterTypes type, int? defaultValue = null)
		{
			Name = name;
			Type = type;
			DefaultValue = defaultValue;
		}
	}

	/// <summary>
	/// Тип макропараметра.
	/// </summary>
	public enum MacroParameterTypes
	{
		Position,
		Key
	}
}
