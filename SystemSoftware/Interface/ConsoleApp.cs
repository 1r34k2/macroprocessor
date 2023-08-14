using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SystemSoftware.Common;
using SystemSoftware.MacroProcessor;
using SystemSoftware.Resources;

namespace SystemSoftware.Interface
{
	public class ConsoleProgram : AbstractApp
	{
		/// <summary>
		/// Закончен ли 1 проход.
		/// </summary>
		public bool IsFirstRunEnded { get; set; }

		/// <summary>
		/// Закончен ли 2 проход.
		/// </summary>
		[Obsolete("Только 1 проход", true)]
		public bool IsSecondRunEnded { get; set; }

		/// <summary>
		/// Идет ли обработка по шагам.
		/// </summary>
		public bool IsProcessingBySteps { get; set; }

		/// <summary>
		/// Закончена ли обработка исходного кода.
		/// </summary>
		public bool IsProcessingEnded { get; set; }

		/// <summary>
		/// Конструктор. Считывает исходники с файла
		/// </summary>
		public ConsoleProgram(string[] args)
		{
			#region Разбор аргyментов командной строки

			switch (args.Length)
			{
				case 1:
					if (args[0].ToUpper() == "-HELP")
					{
						Console.WriteLine(GetUserGuide());
					}
					else
					{
						throw new CustomException(ConsoleMessages.Error_WrongCommandLineArguments);
					}
					break;
				case 2:
					if (args[0].ToUpper() == "-INPUT_FILE")
					{
						InputFile = args[1];
					}
					else if (args[0].ToUpper() == "-OUTPUT_FILE")
					{
						OutputFile = args[1];
					}
					else
					{
						throw new CustomException(ConsoleMessages.Error_WrongCommandLineArguments);
					}
					break;
				case 4:
					if (args[0].ToUpper() == "-INPUT_FILE")
					{
						InputFile = args[1];
						if (args[2].ToUpper() == "-OUTPUT_FILE")
						{
							OutputFile = args[3];
						}
						else
						{
							throw new CustomException(ConsoleMessages.Error_OutputFileArgument);
						}
					}
					else if (args[0].ToUpper() == "-OUTPUT_FILE")
					{
						OutputFile = args[1];
						if (args[2].ToUpper() == "-INPUT_FILE")
						{
							InputFile = args[3];
						}
						else
						{
							throw new CustomException(ConsoleMessages.Error_InputFileArgument);
						}
					}
					else
					{
						throw new CustomException(ConsoleMessages.Error_WrongCommandLineArguments);
					}
					break;
				default:
					throw new CustomException(ConsoleMessages.Error_WrongArgumentsCount);
			}

			#endregion

			Refresh();
		}

		/// <summary>
		/// Обновить приложение.
		/// </summary>
		public void Refresh()
		{
			try
			{
				var temp = File.ReadAllLines(InputFile);
				SourceCode = new Processor(temp);
				SourceStrings = new List<string>(temp);

				//RunMode = RunMode.FirstRun;
				IsFirstRunEnded = false;
				//IsSecondRunEnded = false;
				IsProcessingBySteps = false;
				IsProcessingEnded = false;
				SourceCodeIndex = 0;
			}
			catch (Exception)
			{
				throw new CustomException(ConsoleMessages.Error_InputFileNotFound);
			}
		}


		/// <summary>
		/// Следующий шаг выполнения проги
		/// </summary>
		public void NextStep()
		{
			try
			{
				if (!IsProcessingEnded)
				{
					SourceCode.FirstRunStep(SourceCode.SourceCodeLines[SourceCodeIndex++], MacrosStorage.Root);
				}
			}
			catch (ArgumentOutOfRangeException)
			{
				IsProcessingEnded = true;
			}
			catch (CustomException ex)
			{
				IsProcessingEnded = true;
				Helpers.WriteInConsole($@"{ConsoleMessages.Prefix_ErrorInstring} ""{SourceCode.SourceCodeLines[SourceCodeIndex - 1].ToString()}"": {ex.Message}");
			}
			if (SourceCodeIndex == SourceCode.SourceCodeLines.Count)
			{
				Helpers.WriteInConsole(ConsoleMessages.ApplicationRunEnded);
				IsProcessingEnded = true;
				return;
			}
		}

		/// <summary>
		/// Первый проход
		/// </summary>
		public void FirstRun()
		{
			/*if (IsProcessingBySteps == true)
			{
				Helpers.WriteInConsole(ConsoleMessages.StepModeActivated);
				return;
			}*/
			if (IsFirstRunEnded == true)
			{
				Helpers.WriteInConsole(ConsoleMessages.FirstRunEnded);
				return;
			}
			
			try
			{
				while (true)
				{
					NextStep();
					if (SourceCodeIndex == SourceCode.SourceCodeLines.Count)
					{
						IsFirstRunEnded = true;
						IsProcessingEnded = true;
						return;
					}
					if (IsProcessingEnded) {
                        SourceCodeIndex = 0;
						return;
					}
				}
			}
			catch (CustomException ex)
			{
				IsProcessingEnded = true;
				Helpers.WriteInConsole($"{ConsoleMessages.ErrorPrefix} :{ex.Message}");
			}
		}

		/// <summary>
		/// Возвращает строку со справкой по программе
		/// </summary>
		/// <returns></returns>
		public static string GetUserGuide()
		{
			var n = Environment.NewLine;
			var t = Separator.Tab;
			return
				$"{ConsoleMessages.Help_Title}{n}" +
				$"{t}{ConsoleMessages.Help_AllowedKeys}: [-input_file] [-output_file] [-help]{n}" +
				$"-input_file{t}{ConsoleMessages.Help_InputFileKey}{n}" +
				$"-output_file{t}{ConsoleMessages.Help_OutputFileKey}{n}" +
				$"-help{t}{t}{ConsoleMessages.Help_HelpKey}.{n}" +
				$"{ConsoleMessages.Help_Footer}.{n}";
		}

		/// <summary>
		/// Менюшка консольного приложения.
		/// </summary>
		public static string GetProgramGuide()
		{
			var n = Environment.NewLine;
			var s = Separator.Bracket;
			var t = Separator.Space + Separator.Space;
			return
				$"1{s}{t}{ConsoleMessages.Menu_NextStep}{n}" +
				$"2{s}{t}{ConsoleMessages.Menu_FirstRun}{n}" +
				$"3{s}{t}{ConsoleMessages.Menu_PrintSourceCode}{n}" +
				$"4{s}{t}{ConsoleMessages.Menu_PrintAssemblerCode}{n}" +
				$"5{s}{t}{ConsoleMessages.Menu_PrintVariablesTable}{n}" +
				$"6{s}{t}{ConsoleMessages.Menu_PrintMacrosTable}{n}" +
				$"7{s}{t}{ConsoleMessages.Menu_SaveAssemblerCodeIntoFile}{n}" +
				$"8{s}{t}{ConsoleMessages.Menu_Refresh}{n}" +
				$"9{s}{t}Распечатать таблицу имен макросов{n}" +
				$"0{s}{t}{ConsoleMessages.Menu_Exit}{n}";
		}

		/// <summary>
		/// Распечатывает полностью ассемблерный код в консоль.
		/// </summary>
		public void PrintAssemblerCode()
		{
			Helpers.WriteInConsole(ConsoleMessages.Print_AssemblerCode);
			foreach (var se in SourceCode.AssemblerCode)
			{
				Console.WriteLine(se.ToString());
			}
		}

		/// <summary>
		/// Распечатать ТМО в консоль.
		/// </summary>
		public void PrintTmo()
		{
			Helpers.WriteInConsole(ConsoleMessages.Print_MacrosTable);
			foreach (var e in MacrosStorage.Entities)
			{
				Console.WriteLine($"{Separator.Tab}{e.Name}:");
				for (int i = 0; i < e.Body.Count; i++)
				{
					Console.WriteLine(e.Body[i].ToString());
				}
			}
		}

		/// <summary>
		/// Распечатать таблицу глобальных переменных в консоль.
		/// </summary>
		public void PrintVariablesTable()
		{
			Helpers.WriteInConsole(ConsoleMessages.Print_VariablesTable);
			foreach (Variable e in VariablesStorage.Entities)
			{
				Console.WriteLine($"{e.Name} = {e.Value?.ToString() ?? string.Empty}");
			}
		}


		/// <summary>
		/// Распечатать таблицу имен макросов
		/// </summary>
		public void PrintMacroNameTable()
		{
			Helpers.WriteInConsole("Таблица имен макросов");
			foreach (var e in MacrosStorage.Entities)
			{
				var startIndex = SourceCode.SourceCodeLines.IndexOf(SourceCode.SourceCodeLines.FirstOrDefault(x => x.SourceString.ToUpper().Contains($"{e.Name} MACRO".ToUpper()))) + 1;
				Console.WriteLine($"{e.Name}. Начало: {startIndex}. Длина: {e.Body?.Count ?? 0}");
			}
		}
	}

}
