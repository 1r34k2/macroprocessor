using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SystemSoftware.Common;
using SystemSoftware.Interface;
using SystemSoftware.MacroProcessor;
using SystemSoftware.Resources;

namespace SystemSoftware
{
	public class Program
	{
		#region Поля для скрытия/открытия консоли

		const int SW_HIDE = 0;
		const int SW_SHOW = 5;

		[DllImport("kernel32.dll")]
		static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll")]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		#endregion

		/// <summary>
		/// Главная точка входа для приложения.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			var handle = GetConsoleWindow();

			if (args.Length == 0)
			{
				ShowWindow(handle, SW_HIDE);

				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new MainForm());
			}
			else
			{
				ShowWindow(handle, SW_SHOW);
				try
				{
					ConsoleProgram program = new ConsoleProgram(args);
					Helpers.WriteInConsole(ConsoleProgram.GetProgramGuide());
					string ch = "";
					while ((ch = Console.ReadLine().ToUpper().Trim()) != "0")
					{
						switch (ch)
						{
							case "1":
								if (!program.IsProcessingEnded)
								{
									if (program.IsFirstRunEnded == true)
									{
										Helpers.WriteInConsole(ConsoleMessages.FirstRunEnded);
										break;
									}
									program.IsProcessingBySteps = true;
									Helpers.WriteInConsole(ConsoleMessages.StepExecuted);
									program.NextStep();
								}
								else
								{
									Helpers.WriteInConsole(ConsoleMessages.ApplicationRunEnded);
								}
								break;
							case "2":
								if (!program.IsProcessingEnded)
								{
									program.FirstRun();
									Console.WriteLine();
								}
								else
								{
									Helpers.WriteInConsole(ConsoleMessages.ApplicationRunEnded);
								}
								break;
							case "3":
								Helpers.WriteInConsole(ConsoleMessages.Menu_PrintSourceCode);
								foreach (string str in program.SourceStrings)
								{
									Console.WriteLine(str);
								}
								Console.WriteLine();
								break;
							case "4":
								Helpers.WriteInConsole(ConsoleMessages.Menu_PrintAssemblerCode);
								program.PrintAssemblerCode();
								Console.WriteLine();
								break;
							case "5":
								Helpers.WriteInConsole(ConsoleMessages.Menu_PrintVariablesTable);
								program.PrintVariablesTable();
								Console.WriteLine();
								break;
							case "6":
								Helpers.WriteInConsole(ConsoleMessages.Menu_PrintMacrosTable);
								program.PrintTmo();
								Console.WriteLine();
								break;
							case "8":
								Helpers.WriteInConsole(ConsoleMessages.ApplicationRefreshed);
								MacrosStorage.Refresh();
								VariablesStorage.Refresh();
								program = new ConsoleProgram(args);
								program.SourceCode.AssemblerCode = new List<CodeEntity>();
								Console.WriteLine();
								break;

							case "9":
								Helpers.WriteInConsole("Распечатать таблицу имен макросов");
								program.PrintMacroNameTable();
								Console.WriteLine();
								break;
							case "7":
								try
								{
									StreamWriter sw = new StreamWriter(program.OutputFile);
									foreach (CodeEntity se in program.SourceCode.AssemblerCode)
									{
										sw.WriteLine(se.ToString());
									}
									sw.Close();
									Helpers.WriteInConsole(ConsoleMessages.SaveIntoFileSuccess);
									Process.Start("notepad.exe", program.OutputFile);
								}
								catch
								{
									Helpers.WriteInConsole(ConsoleMessages.Error_OutputFileNotFound);
								}
								break;
							default:
								Helpers.WriteInConsole(ConsoleMessages.WrongKeyEntered);
								break;
						}
						Helpers.WriteInConsole(ConsoleProgram.GetProgramGuide());
					}
				}
				catch (CustomException ex)
				{
					Helpers.WriteInConsole($"{ConsoleMessages.ErrorPrefix}: {ex.Message}");
					Helpers.WriteInConsole(ConsoleProgram.GetUserGuide());
					Helpers.WriteInConsole(ConsoleMessages.ApplicationRunEnded);
				}
				catch (Exception ex)
				{
					Helpers.WriteInConsole($"{ConsoleMessages.ErrorPrefix}: {ex.Message}");
					Helpers.WriteInConsole(ConsoleProgram.GetUserGuide());
					Helpers.WriteInConsole(ConsoleMessages.ApplicationRunEnded);
				}
			}
		}
	}
}
