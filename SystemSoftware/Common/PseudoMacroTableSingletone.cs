using System.Collections.Generic;
using System.Linq;
using SystemSoftware.MacroProcessor;

namespace SystemSoftware.Common
{
	/// <summary> 
	/// List of pseudo macro source entities
	/// </summary>
	public class PseudoMacroTableSingletone
	{
		private static List<CodeEntity> instance;

		private PseudoMacroTableSingletone() { }

		public static List<CodeEntity> Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new List<CodeEntity>();
				}
				return instance;
			}
		}

		/// <summary> 
		/// Add some source entity into the pseudo macros list
		/// </summary>
		public static void AddSourceEntity(CodeEntity se)
		{
			Instance.Add(se);
		}

		/// <summary> 
		/// Remove some source entity from the pseudo macros list
		/// </summary>
		public static void RemoveSourceEntity(CodeEntity se)
		{
			Instance.Remove(se);
		}

		/// <summary> 
		/// Clear pseudo macro list
		/// </summary>
		public static void Clear()
		{
			instance = null;
		}

		/// <summary> 
		/// Check all the source entities from the pseudo macro list, if they are macro call or not
		/// </summary>
		public static void CheckPseudoMacro()
		{
			foreach (CodeEntity se in Instance)
			{
				if (!MacrosStorage.IsInTMO(se.Operation) &&
					!VariablesStorage.IsInVariablesStorage(se.Operation) &&
					!Helpers.IsAssemblerDirective(se.Operation) &&
					!Helpers.IsKeyWord(se.Operation) &&
					!Helpers.MacroGenerationDirectives.Contains(se.Operation) &&
					!(se.Operands.Count > 0 && se.Operands[0] == "START"))
				{
					throw new CustomException("Операция \"" + se.Operation + "\" не является ни оператором языка Ассемблера, ни оператором Макроязыка, ни макросом.");
				}
			}
		}
	}
}
