using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SystemSoftware.Common;
using SystemSoftware.MacroProcessor;

namespace SystemSoftware.Interface
{
	/// <summary>
	/// Приложение с GUI интерфейсом.
	/// </summary>
	public class VisualApp : AbstractApp
	{
		/// <summary>
		/// Конструктор. Считывает исходники с файла.
		/// </summary>
		public VisualApp(IEnumerable<string> sourceCodeText)
		{
			RefreshApplication(sourceCodeText);
		}

		/// <summary>
		/// Обновляет результаты предыдущего прохода
		/// </summary>
		private void RefreshApplication(IEnumerable<string> sourceCodeText)
		{
			MacrosStorage.Refresh();
			VariablesStorage.Refresh();
			SourceCode = new Processor(sourceCodeText);
			SourceStrings = new List<string>(sourceCodeText);
		}

		/// <summary>
		/// Шаг выполнения программы 1 просмотра
		/// </summary>
		public void NextFirstStep(TextBox tb)
		{
			try
			{
				SourceCode.FirstRunStep(SourceCode.SourceCodeLines[SourceCodeIndex++], MacrosStorage.Root);
			}
			catch (ArgumentOutOfRangeException)
			{
				SourceCodeIndex = 0;
				RefreshApplication(SourceStrings.ToArray());
				MacrosStorage.Refresh();
				VariablesStorage.Refresh();
			}
			catch (CustomException ex)
			{
				throw new CustomException($"{ex.Message}");
			}
			catch (Exception e)
			{
				throw new CustomException($"{e.Message}");
			}
		}

		#region Распечатка

		/// <summary>
		/// Печатает исходники SourceStrings в TextBox
		/// </summary>
		public void PrintSourceCode(RichTextBox tb)
		{
			tb.Clear();
			foreach (string str in SourceStrings)
			{
				tb.AppendText(str + Environment.NewLine);
			}
		}

		/// <summary>
		/// Распечатывает полностью ассемблерный код без макросов в таблицу
		/// </summary>
		public void PrintAssemblerCode(TextBoxBase tb)
		{
			tb.Clear();
			foreach (var se in SourceCode.AssemblerCode)
			{
				tb.AppendText(se.ToString().ToUpper() + Environment.NewLine);
			}
		}

		/// <summary>
		/// Распечатать ТМО в таблицу
		/// </summary>
		public void PrintTmo(DataGridView dgv)
		{
			dgv.Rows.Clear();
			for (int i = 0; i < dgv.Rows.Count; i++)
			{
				dgv.Rows.Remove(dgv.Rows[i]);
			}
			foreach (var e in MacrosStorage.Entities)
			{
				dgv.Rows.Add(e.Name, e.Body.Count > 0 ? e.Body[0].ToString() : "");
				for (int i = 1; i < e.Body.Count; i++)
				{
					dgv.Rows.Add(null, e.Body[i].ToString());
				}
			}
		}

		/// <summary>
		/// Распечатать таблицу глобальных переменных в GUI таблицу.
		/// </summary>
		public void PrintVariablesTable(DataGridView dgv)
		{
			dgv.Rows.Clear();
			for (int i = 0; i < dgv.Rows.Count; i++)
			{
				dgv.Rows.Remove(dgv.Rows[i]);
			}
			foreach (var e in VariablesStorage.Entities)
			{
				dgv.Rows.Add(e.Name, e.Value != null ? e.Value.ToString() : "");
			}
		}


		/// <summary>
		/// Распечатать таблицу имен макросов
		/// </summary>
		public void PrintMacroNameTable(DataGridView dgv)
		{
			dgv.Rows.Clear();
			for (int i = 0; i < dgv.Rows.Count; i++)
			{
				dgv.Rows.Remove(dgv.Rows[i]);
			}
			foreach (var e in MacrosStorage.Entities)
			{
				dgv.Rows.Add(e.Name, SourceCode.SourceCodeLines.IndexOf(SourceCode.SourceCodeLines.FirstOrDefault(x => x.SourceString.ToUpper().Contains($"{e.Name} MACRO".ToUpper()))) + 1, e.Body?.Count ?? 0);
			}
		}

		/// <summary>
		/// Распечатать список директив макропроцессора (статический).
		/// </summary>
		/// <param name="tbAssemblerDirectives">Текстовое поле для директив ассемблера.</param>
		/// <param name="tbMacrogenerationDirectives">Текстовое поле для директив макрогенерации.</param>
		public void PrintMacroprocessorDirectives(TextBoxBase tbAssemblerDirectives, TextBoxBase tbMacrogenerationDirectives)
		{
			var assemblerDirectives = Helpers.AssemblerDirectives
				.Concat(Helpers.Keywords);
			var macrogenerationDirectives = Helpers.MacroGenerationDirectives
				.Concat(Helpers.ConditionDirectives)
				.Concat(Helpers.VariableDirectives);

			tbAssemblerDirectives.Text = string.Join(", ", assemblerDirectives);
			tbMacrogenerationDirectives.Text = string.Join(", ", macrogenerationDirectives);
		}

		#endregion
	}
}
