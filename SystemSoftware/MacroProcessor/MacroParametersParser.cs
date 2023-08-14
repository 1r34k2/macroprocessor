using System;
using System.Collections.Generic;
using System.Linq;
using SystemSoftware.Common;
using SystemSoftware.Resources;

namespace SystemSoftware.MacroProcessor
{
	/// <summary>
	/// Утилита для парсинга параметров из строки.
	/// </summary>
	public abstract class MacroParametersParser
	{
		protected readonly Processor Processor;

		public MacroParametersParser(Processor processor)
		{
			Processor = processor;
		}

		/// <summary>
		/// Распарсить строку в параметры.
		/// </summary>
		/// <param name="operands">Операнды.</param>
		/// <returns>Список параметров.</returns>
		public abstract List<MacroParameter> Parse(List<string> operands, string macroName);
	}

	/// <summary>
	/// Утилита для парсинга позиционных параметров.
	/// </summary>
	public class PositionMacroParametersParser : MacroParametersParser
	{
		public PositionMacroParametersParser(Processor processor)
			: base(processor)
		{

		}

		/// <summary>
		/// Распарсить строку в параметры.
		/// </summary>
		/// <param name="operands">Операнды.</param>
		/// <returns>Список параметров.</returns>
		public override List<MacroParameter> Parse(List<string> operands, string macroName)
		{
			if (!(operands?.Any() ?? false))
			{
				return new List<MacroParameter>();
			}

			// Проверить корректность имени
			foreach (string currentOperand in operands)
			{
				Helpers.CheckNames(currentOperand);

				if (operands.Count(x => x == currentOperand) > 1)
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.MacroParameterDublicate, currentOperand));
				}
				if (!Helpers.IsLabel(currentOperand))
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroParameterName, currentOperand));
				}
			}

			return operands
				.Select(e => new MacroParameter(e, MacroParameterTypes.Position))
				.ToList();
		}
	}

	/// <summary>
	/// Утилита для парсинга ключевых параметров.
	/// </summary>
	public class KeyMacroParametersParser : MacroParametersParser
	{
		public KeyMacroParametersParser(Processor processor)
			: base(processor)
		{

		}

		/// <summary>
		/// Распарсить строку в параметры.
		/// </summary>
		/// <param name="operands">Операнды.</param>
		/// <returns>Список параметров.</returns>
		public override List<MacroParameter> Parse(List<string> operands, string macroName)
		{
			if (!(operands?.Any() ?? false))
			{
				return new List<MacroParameter>();
			}

			var parameters = new List<MacroParameter>();

			// Проверить корректность имени
			foreach (string currentOperand in operands)
			{
				Helpers.CheckNames(currentOperand);

				var vals = currentOperand.Split('=');
				if (vals.Length != 2)
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroDefinitionParameter, currentOperand));
				}
				var parameterName = vals[0];
				var defaultValue = vals[1];

				if (parameters.Any(e => e.Name == parameterName))
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.MacroParameterDublicate, currentOperand));
				}
				if (!Helpers.IsLabel(parameterName))
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroDefinitionParameter, currentOperand));
				}

				//if (!string.IsNullOrEmpty(defaultValue))
				//{
				//	throw new CustomException(string.Format(ProcessorErrorMessages.IcorrectMacroDefinitionParameter, currentOperand));
				//}

				if (!string.IsNullOrEmpty(defaultValue) && !int.TryParse(defaultValue, out int temp))
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroDefinitionParameterDefault, currentOperand));
				}

				parameters.Add(new MacroParameter
				{
					Name = parameterName,
					Type = MacroParameterTypes.Key,
					DefaultValue = !string.IsNullOrEmpty(defaultValue)
						? int.Parse(defaultValue)
						: (int?)null
				});
			}

			return parameters;
		}
	}

	/// <summary>
	/// Утилита для парсинга смешанных параметров.
	/// </summary>
	public class MixedMacroParametersParser : MacroParametersParser
	{
		public MixedMacroParametersParser(Processor processor)
			: base(processor)
		{

		}

		/// <summary>
		/// Распарсить строку в параметры.
		/// </summary>
		/// <param name="operands">Операнды.</param>
		/// <returns>Список параметров.</returns>
		public override List<MacroParameter> Parse(List<string> operands, string macroName)
		{
			if (!(operands?.Any() ?? false))
			{
				return new List<MacroParameter>();
			}

			var parameters = new List<MacroParameter>();

			var firstKeyParameterIdx = Array.IndexOf(operands.ToArray(), operands.FirstOrDefault(x => x.Contains("=")));
			if (firstKeyParameterIdx != -1)
			{
				if (operands.Any(x => !x.Contains("=") && Array.IndexOf(operands.ToArray(), x) > firstKeyParameterIdx))
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroDefinitionParameters, macroName));
				}
			}

			// Проверить корректность позиционных параметров
			var lastPositionParameterIdx = firstKeyParameterIdx >= 0 ? firstKeyParameterIdx : operands.Count();
			for (int i = 0; i < lastPositionParameterIdx; i++)
			{
				var currentOperand = operands.ToArray()[i];

				Helpers.CheckNames(currentOperand);

				if (parameters.Any(e => e.Name == currentOperand))
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.MacroParameterDublicate, currentOperand));
				}
				if (!Helpers.IsLabel(currentOperand))
				{
					throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroParameterName, currentOperand));
				}

				parameters.Add(new MacroParameter
				{
					Name = currentOperand,
					Type = MacroParameterTypes.Position
				});
			}

			// Проверить корректность ключевых параметров
			if (firstKeyParameterIdx >= 0)
			{
				for (int i = firstKeyParameterIdx; i < operands.Count(); i++)
				{
					var currentOperand = operands.ToArray()[i];

					Helpers.CheckNames(currentOperand);

					var vals = currentOperand.Split('=');
					if (vals.Length != 2)
					{
						throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroDefinitionParameter, currentOperand));
					}
					var parameterName = vals[0];
					var defaultValue = vals[1];

					if (parameters.Any(e => e.Name == parameterName))
					{
						throw new CustomException(string.Format(ProcessorErrorMessages.MacroParameterDublicate, currentOperand));
					}
					if (!Helpers.IsLabel(parameterName))
					{
						throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroDefinitionParameter, currentOperand));
					}

					//if (!string.IsNullOrEmpty(defaultValue))
					//{
					//	throw new CustomException(string.Format(ProcessorErrorMessages.IcorrectMacroDefinitionParameter, currentOperand));
					//}

					if (!string.IsNullOrEmpty(defaultValue) && !int.TryParse(defaultValue, out int temp))
					{
						throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroDefinitionParameterDefault, currentOperand));
					}

					parameters.Add(new MacroParameter
					{
						Name = parameterName,
						Type = MacroParameterTypes.Key,
						DefaultValue = !string.IsNullOrEmpty(defaultValue)
							? int.Parse(defaultValue)
							: (int?)null
					});
				}
			}

			return parameters;
		}
	}
}
