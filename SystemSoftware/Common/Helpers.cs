using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SystemSoftware.MacroProcessor;
using SystemSoftware.Resources;

namespace SystemSoftware.Common
{
    public static class Helpers
    {
        public static readonly string[] Symbols = {"%", "#", "$", "", "!", "@", "^", "&", "*", "-", "[", "\"", "*", "(", ")", "\\",
            "/", "?", "№", ";", ":", "+", "=", "[", "]", "{", "}", "|", "<", ">", "`", "~", ".", ",", "'", " " };
        public static readonly string[] AssemblerDirectives = { "BYTE", "RESB", "RESW", "WORD" };
        public static readonly string[] MacroGenerationDirectives = { "START", "END", "MACRO", "MEND" };
        public static readonly string[] ConditionDirectives = { Directives.While, Directives.Endw, Directives.If, Directives.Else, Directives.EndIf };
        public static readonly string[] VariableDirectives = { Directives.Variable, Directives.Set, Directives.Increment };
        public static readonly string[] Keywords = { "ADD", "SAVER1", "SAVER2", "LOADR1", "JMP" };
        public static readonly string[] AllKeywords = MacroGenerationDirectives.Concat(ConditionDirectives).Concat(VariableDirectives).Concat(Keywords).ToArray();
        public static readonly string RussianLetters = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
        public static readonly string[] ComparisonSigns = { "==", ">=", "<=", "!=", ">", "<" };
        public static readonly string CurrentDirectory = new DirectoryInfo(Environment.CurrentDirectory).Parent.Parent.Parent.FullName;

        /// <summary>
        /// Проверка на директивы препроцессора (ассемблера).
        /// </summary>
        /// <param name="operation">Строка для проверки.</param>
        /// <returns>Является ли операция директивой ассемблера.</returns>
        public static bool IsAssemblerDirective(string operation)
        {
            return operation != null && operation.In(AssemblerDirectives);
        }

        /// <summary>
        /// Проверка на метку.
        /// </summary>
        public static bool IsLabel(string label)
        {
            if (!IsOperation(label))
            {
                return false;
            }
            if (AllKeywords.Contains(label))
            {
                return false;
            }
            if (!IsNotRussian(label))
            {
                return false;
            }
            if (MacrosStorage.IsInTMO(label))
            {
                return false;
            }
            if (VariablesStorage.IsInVariablesStorage(label))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Проверка на операцию.
        /// </summary>
        public static bool IsOperation(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return false;

            // Should not begin with digit
            if (char.IsDigit(operation[0])) return false;

            // Should not contain incorrect symbols like #, $, ...
            if (operation.Any(x => x.ToString().In(Symbols))) return false;

            // Should not be a register
            if (IsRegister(operation)) return false;

            // Should not be a directive
            if (IsAssemblerDirective(operation)) return false;

            return true;
        }

        /// <summary> 
        /// Проверка на ключевое слово.
        /// </summary>
        /// <param name="operation">Операция для проверки.</param>
        /// <returns>Является ли операция ключевым словом.</returns>
        public static bool IsKeyWord(string operation)
        {
            return operation != null && operation.In(Keywords);
        }

        /// <summary> 
        /// Проверка на регистр.
        /// </summary>
        /// <param name="operation">Операция для проверки.</param>
        /// <returns>Является ли операция регистром.</returns>
        public static bool IsRegister(string operation)
        {
            for (int i = 0; i < 16; i++)
            {
                if ("R" + i.ToString() == operation.Trim().ToUpper())
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Проверка на присутствие русских символов.
        /// </summary>
        public static bool IsNotRussian(string word)
        {
            return !word.Any(x => x.ToString().In(RussianLetters));
        }

        #region Comparison coditions for IF-AIF-WHILE

        /// <summary>
        /// Get condition parts from string like "a > b"
        /// </summary>
        public static void ParseCondition(string str, out int first, out int second, out string sign)
        {
            string[] arr;
            first = 0;
            second = 0;
            sign = "";
            int temp;
            foreach (string sgn in ComparisonSigns)
            {
                if ((arr = str.Split(new string[] { sgn }, StringSplitOptions.None)).Length > 1)
                {
                    if (VariablesStorage.IsInVariablesStorage(arr[0]))
                    {
                        if (VariablesStorage.Find(arr[0]).Value == null)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.EmptyVariableInComparison} ({arr[0]})");
                        }
                        else
                        {
                            first = (int)VariablesStorage.Find(arr[0]).Value;
                        }
                    }
                    else if (int.TryParse(arr[0], out temp) == false)
                    {
                        throw new CustomException($"{ProcessorErrorMessages.UndefinedComparisonPart} ({arr[0]})");
                    }
                    else
                    {
                        first = int.Parse(arr[0]);
                    }

                    if (VariablesStorage.IsInVariablesStorage(arr[1]))
                    {
                        if (VariablesStorage.Find(arr[1]).Value == null)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.EmptyVariableInComparison} ({arr[1]})");
                        }
                        else
                        {
                            second = (int)VariablesStorage.Find(arr[1]).Value;
                        }
                    }
                    else if (int.TryParse(arr[1], out temp) == false)
                    {
                        throw new CustomException($"{ProcessorErrorMessages.UndefinedComparisonPart} ({arr[1]})");
                    }
                    else
                    {
                        second = int.Parse(arr[1]);
                    }

                    sign = sgn;
                    return;
                }
            }
            throw new CustomException(ProcessorErrorMessages.UndefinedComparisonSign);
        }

        public static void PushConditionArgs(string str, Macro te)
        {
            int first, second;
            string sign;
            Helpers.ParseCondition(str, out first, out second, out sign);
            string[] arr;
            List<string> list = new List<string>();
            if ((arr = str.Split(new string[] { sign }, StringSplitOptions.None)).Length == 2)
            {
                if (VariablesStorage.IsInVariablesStorage(arr[0]))
                {
                    list.Add(arr[0]);
                }
                if (VariablesStorage.IsInVariablesStorage(arr[1]))
                {
                    list.Add(arr[1]);
                }
            }
            Dictionary<List<string>, Macro> dict = new Dictionary<List<string>, Macro>();
            dict.Add(list, te);
            VariablesStorage.WhileVar.Push(dict);
        }


        /// <summary> Сравнение
        /// </summary>
        public static bool Compare(string str)
        {
            int first;
            int second;
            string sign;
            ParseCondition(str, out first, out second, out sign);
            switch (sign)
            {
                case ">":
                    return first > second;
                case "<":
                    return first < second;
                case ">=":
                    return first >= second;
                case "<=":
                    return first <= second;
                case "==":
                    return first == second;
                case "!=":
                    return first != second;
                default:
                    return false;
            }
        }

        #endregion

        /// <summary>
        /// Проверка на совпадение имен.
        /// </summary>
        /// <param name="name">Имя для проверки.</param>
        public static void CheckNames(string name)
        {
            List<string> list = new List<string>();
            foreach (Variable glob in VariablesStorage.Entities)
            {
                list.Add(glob.Name);
            }
            foreach (Macro te in MacrosStorage.Entities)
            {
                list.Add(te.Name);
            }
            foreach (var p in MacrosStorage.Entities.SelectMany(e => e.Parameters))
            {
                list.Add(p.Name);
            }
            if (list.Contains(name))
            {
                throw new CustomException($"{ProcessorErrorMessages.NameIsAleradyUsed} ({name})");
            }
        }

        public static CodeEntity Print(CodeEntity str)
        {
            CodeEntity newStr = str.Clone() as CodeEntity;
            for (int j = 0; j < newStr.Operands.Count; j++)
            {
                if (VariablesStorage.IsInVariablesStorage(newStr.Operands[j]))
                {
                    if (VariablesStorage.Find(newStr.Operands[j]).Value.HasValue)
                        newStr.Operands[j] = VariablesStorage.Find(newStr.Operands[j]).Value.Value.ToString();
                    else
                        throw new CustomException($"{ProcessorErrorMessages.NullVariable} ({newStr.Operands[j]})");
                }
            }
            return newStr;
        }

        /// <summary>
        /// Записать сообщение в консоль с разделителями в виде новой строки.
        /// </summary>
        /// <param name="message">Строка, которою надо записать в консоль.</param>
        public static void WriteInConsole(string message)
        {
            Console.WriteLine($"{Environment.NewLine}{message}{Environment.NewLine}");
        }
    }

    public class CustomException : Exception
    {
        public CustomException(string message)
            : base(message)
        {
        }
    }
}
