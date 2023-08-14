using System;
using System.Collections.Generic;
using System.Linq;
using SystemSoftware.Common;
using SystemSoftware.Resources;

namespace SystemSoftware.MacroProcessor
{
    /// <summary>
    /// Процессор. Обработка макрогенерации.
    /// </summary>
    public class Processor
    {
        public static string ConditionDirectives = "IF,ELSE,ENDIF,AIF,AGO";
        /// <summary>
        /// Вложенность макроопределений.
        /// </summary>
        private int _macroCount { get; set; }

        /// <summary>
        /// Название текущего макроопределения.
        /// </summary>
        public string _currentMacroName { get; set; }

        /// <summary>
        /// Список строк, подозрительных на макровызов.
        /// </summary>
        private List<CodeEntity> PseudoMacroCalls { get; set; } = new List<CodeEntity>();

        /// <summary>
        /// Список строк исходного кода.
        /// </summary>
        public List<CodeEntity> SourceCodeLines { get; set; }

        /// <summary>
        /// Результаты первого прохода - ассемблерный код.
        /// </summary>
        public List<CodeEntity> AssemblerCode { get; set; } = new List<CodeEntity>();

        /// <summary>
        /// Парсер параметров.
        /// </summary>
        public readonly MacroParametersParser MacroParametersParser;

        public static string cm;

        public static List<StructMarks> assemblyMarks = new List<StructMarks>();

        public Processor(IEnumerable<string> strs)
        {
            //парсим строки в объектное представление
            SourceCodeLines = ParseSourceCode(strs);
            //назначаем родителя для исходных строк
            foreach (CodeEntity se in SourceCodeLines)
            {
                se.Sources = this;
            }
            PseudoMacroTableSingletone.Clear();
            MacroParametersParser = new MixedMacroParametersParser(this);
        }

        public Processor(List<CodeEntity> strs)
        {
            //парсим строки в объектное представление
            SourceCodeLines = strs;
            //назначаем родителя для исходных строк
            foreach (CodeEntity se in SourceCodeLines)
            {
                se.Sources = this;
            }
            MacroParametersParser = new MixedMacroParametersParser(this);
        }

        public void MacroSubstitution(CodeEntity se, Macro te)
        {
            List<CodeEntity> localMbMacroCall = new List<CodeEntity>();
            foreach (CodeEntity mc in PseudoMacroCalls)
            {
                if (mc.Operation == _currentMacroName)
                {
                    Macro currentTe = MacrosStorage.SearchInTMO(_currentMacroName);
                    currentTe.PreviousMacros = te;

                    CheckSourceEntity.CheckMacroSubstitution(mc, currentTe);
                    CheckBody.CheckMacroRun(mc, te, currentTe);

                    // Обработаем параметры макроса
                    var processedMacroBody = ProcessMacroParams(currentTe, mc.Operands);

                    var calledProcessor = currentTe.CallMacro(processedMacroBody);
                    //localMbMacroCall.AddRange(calledProcessor.PseudoMacroCalls);

                    // результат макроподстановки
                    List<CodeEntity> macroSubs = new List<CodeEntity>();
                    foreach (CodeEntity str in calledProcessor.AssemblerCode)
                    {
                        CodeEntity macroSubsEntity = Helpers.Print(str);
                        macroSubs.Add(macroSubsEntity);
                        if (!Helpers.IsAssemblerDirective(macroSubsEntity.Operation) && !Helpers.IsKeyWord(macroSubsEntity.Operation))
                        {
                            localMbMacroCall.Add(macroSubsEntity);
                            macroSubsEntity.IsRemove = true;
                            List<CodeEntity> list = PseudoMacroCalls.FindAll(x => x.Operation == macroSubsEntity.Operation);
                            if (list.Count > 0)
                            {
                                int n = list.Max(x => x.CallNumber);
                                macroSubsEntity.CallNumber = n++;
                            }
                        }
                    }
                    // Заменяем в результате макровызов на результат макроподстановки
                    for (int i = 0; i < AssemblerCode.Count; i++)
                    {
                        if (AssemblerCode[i].Operation == mc.Operation && AssemblerCode[i].IsRemove == true && mc.CallNumber == AssemblerCode[i].CallNumber)
                        {
                            AssemblerCode.Remove(AssemblerCode[i]);
                            AssemblerCode.InsertRange(i, macroSubs);
                            i += macroSubs.Count - 1;
                        }
                    }
                }
            }
            foreach (CodeEntity str in localMbMacroCall)
            {
                PseudoMacroCalls.Add(str);
                PseudoMacroTableSingletone.AddSourceEntity(se);
            }

            foreach (CodeEntity sourceEntity in PseudoMacroCalls)
            {
                Macro currentTe = MacrosStorage.SearchInTMO(sourceEntity.Operation);
                if (MacrosStorage.IsInTMO(sourceEntity.Operation))
                {
                    CheckBody.CheckMacroRun(sourceEntity, te, currentTe);
                }
            }
        }

        /// <summary>
        /// Шаг первого прохода
        /// </summary>
        public void FirstRunStep(CodeEntity se, Macro te)
        {
            string operation = se.Operation;
            string label = se.Label;
            List<string> operands = se.Operands;

            if (label != null && operation == null)
                throw new CustomException("Метка не может быть описана без директивы ассемблера");

            CheckSourceEntity.CheckLabel(se);
            if (operation == "END")
            {
                CheckSourceEntity.CheckEnd(se, _macroCount);
                PseudoMacroTableSingletone.CheckPseudoMacro();
                AssemblerCode.Add(Helpers.Print(se));
            }
            else if (operation == Directives.Variable && _macroCount == 0)
            {
                CheckSourceEntity.CheckVariable(se);
                if (operands.Count == 1)
                    VariablesStorage.Entities.Add(new Variable(operands[0], null));
                else
                    VariablesStorage.Entities.Add(new Variable(operands[0], int.Parse(operands[1])));
            }
            else if (operation == "SET" && _macroCount == 0)
            {
                CheckSourceEntity.CheckSet(se, te);
                VariablesStorage.Find(se.Operands[0]).Value = int.Parse(se.Operands[1]);
            }
            else if (operation == "INC" && _macroCount == 0)
            {
                CheckSourceEntity.CheckInc(se, te);
                VariablesStorage.Find(operands[0]).Value++;
            }
            else if (operation == "MACRO")
            {
                if (_macroCount == 0)
                {
                    CheckSourceEntity.CheckMacro(se, _macroCount);
                    Macro currentTe = new Macro()
                    {
                        Name = label,
                        Parameters = MacroParametersParser.Parse(operands, label)
                    };
                    currentTe.ParentMacros = te;
                    te.ChildrenMacros.Add(currentTe);

                    MacrosStorage.Entities.Add(currentTe);
                    CheckMacros.CheckLocalTmo();
                    _currentMacroName = label;

                }
                else if (_macroCount > 0)
                {
                    MacrosStorage.SearchInTMO(_currentMacroName).Body.Add(se); 
                }
                else
                {
                    throw new CustomException(ProcessorErrorMessages.IncorrectMacroMendCount);
                }
                _macroCount++;

            }
            else if (operation == "MEND")
            {
                
                if (_macroCount > 1)
                {
                    MacrosStorage.SearchInTMO(_currentMacroName).Body.Add(se);
                }
                CheckSourceEntity.CheckMend(se, _macroCount);
                _macroCount--;

                if (_macroCount == 0)
                {
                    MacroSubstitution(se, te);
                }
            }
            else
            {
                
                if (_macroCount > 0)
                {
                    MacrosStorage.SearchInTMO(_currentMacroName).Body.Add(se);

                    if(se.ToString().Split(' ')[0].Contains(':') || se.ToString().Split(' ')[0].Contains('%'))
                        MacrosStorage.macros.Add(se.ToString().Split(' ')[1]);
                    else
                        MacrosStorage.macros.Add(se.ToString().Split(' ')[0]);

                    for (int i = 0; i < MacrosStorage.Entities.Count; i++)
                    {
                        for (int j = 0; j < MacrosStorage.macros.Count; j++)
                        {
                            if (MacrosStorage.Entities[i].Name == MacrosStorage.macros[j])
                                throw new CustomException("Макровызовы запрещены в макросах");
                        }
                    }
                }
                else
                {
                    if (te == MacrosStorage.Root && operation.In("IF", "ELSE", "ENDIF", "WHILE", "ENDW"))
                    {
                        throw new CustomException($"{ProcessorErrorMessages.UseOnlyInsideMacro_1} {operation} {ProcessorErrorMessages.UseOnlyInsideMacro_2} ({se.SourceString})");
                    }
                    
                    // макровызов
                    if (MacrosStorage.IsInTMO(operation))
                    {
                        
                        Macro currentTe = MacrosStorage.SearchInTMO(operation);
                        currentTe.PreviousMacros = te;

                        CheckSourceEntity.CheckMacroSubstitution(se, currentTe);
                        CheckBody.CheckMacroRun(se, te, currentTe);

                        // Обработаем параметры макроса
                        var processedMacroBody = ProcessMacroParams(currentTe, operands);

                        var calledProcessor = currentTe.CallMacro(processedMacroBody);

                        // Если после макровызова есть описанный, но не вызванный макрос - ошибка
                        foreach (CodeEntity sourceEntity in PseudoMacroCalls)
                        {
                            Macro teCur = MacrosStorage.SearchInTMO(sourceEntity.Operation);
                            if (MacrosStorage.IsInTMO(sourceEntity.Operation))
                            {
                                CheckBody.CheckMacroRun(sourceEntity, te, teCur);
                            }
                        }


                        PseudoMacroCalls.AddRange(calledProcessor.PseudoMacroCalls);
                        foreach (CodeEntity str in calledProcessor.AssemblerCode)
                        {
                            AssemblerCode.Add(Helpers.Print(str));
                        }
                    }
                    else
                    {
                        // Добавляем строку в список подозрительных на макровызов и в результат
                        se = Helpers.Print(se);
                        if (_macroCount == 0 && !Helpers.IsAssemblerDirective(se.Operation) && !Helpers.IsKeyWord(se.Operation))
                        {
                            se.IsRemove = true;
                            List<CodeEntity> list = PseudoMacroCalls.FindAll(x => x.Operation == se.Operation);
                            if (list.Count > 0)
                            {
                                int n = list.Max(x => x.CallNumber);
                                se.CallNumber = n + 1;
                            }
                            PseudoMacroCalls.Add(se);
                            PseudoMacroTableSingletone.AddSourceEntity(se);
                        }
                        if (se.Label != null && se.Label.Contains('%'))
                            se.Label = "";
                        AssemblerCode.Add(se);
                    }
                }



            }

            for (int i = 0; i < MacrosStorage.Entities.Count; i++)
            {
                for (int j = 0; j < MacrosStorage.macros.Count; j++)
                {
                    if (MacrosStorage.Entities[i].Name == MacrosStorage.macros[j])
                        throw new CustomException("Макровызовы запрещены в макросах");
                }
            }
        }

        /// <summary>
        /// Парсит массив строк в масссив SourceEntity, но только до появления первого END в качестве операции
        /// </summary>
        public static List<CodeEntity> ParseSourceCode(IEnumerable<string> strs)
        {
            List<CodeEntity> result = new List<CodeEntity>();
            foreach (string s in strs)
            {
                // пропускаем пустую строку
                if (String.IsNullOrEmpty(s.Trim()))
                    continue;
                string currentString = s.ToUpper().Trim();
                CodeEntity se = new CodeEntity() { SourceString = currentString };

                //разборка метки
                if ((currentString.Split(' ')[0].Trim().IndexOf(":") == currentString.Split(' ')[0].Trim().Length - 1) && (!currentString.Contains("BYTE") || currentString.IndexOf(':') < currentString.IndexOf("C'")) && !currentString.Split(' ')[0].Contains('%'))
                {
                    se.Label = currentString.Split(':')[0].Trim();
                    currentString = currentString.Remove(0, currentString.Split(':')[0].Length + 1).Trim();
                }
                else
                {
                    if (currentString.IndexOf("%") == 0)
                    {
                        se.Label = currentString.Split(' ')[0].Trim();
                        var g = new StructMarks()
                        {
                            Name = se.Label,
                            Used = false,
                            Macro = cm
                        };

                        assemblyMarks.Add(g);
                        currentString = currentString.Remove(0, currentString.Split(' ')[0].Length).Trim();
                    }
                }

                if (currentString.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).Length > 0)
                {
                    se.Operation = currentString.Split(null as char[], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    currentString = currentString.Remove(0, currentString.Split(null as char[], StringSplitOptions.RemoveEmptyEntries)[0].Length).Trim();
                }

                if (se.Operation == "BYTE")
                {
                    se.Operands.Add(currentString.Trim());
                }
                else
                {
                    for (int i = 0; i < currentString.Split(null as char[], StringSplitOptions.RemoveEmptyEntries).Length; i++)
                    {
                        se.Operands.Add(currentString.Split(null as char[], StringSplitOptions.RemoveEmptyEntries)[i].Trim());
                    }
                }

                //название проги или макроса - в поле метки
                if (se.Operands.Count > 0 && se.Operands[0] == "MACRO")
                {
                    se.Label = se.Operation;
                    se.Operation = se.Operands[0];

                    cm = se.Label;

                    for (int i = 1; i < se.Operands.Count; i++)
                    {
                        se.Operands[i - 1] = se.Operands[i];
                    }
                    se.Operands.RemoveAt(se.Operands.Count - 1);
                }
                result.Add(se);

                //Читаем только до энда
                if (se.Operation == "END")
                {
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Произвести подстановку параметров при макровызове.
        /// </summary>
        /// <param name="macro">Макрос.</param>
        /// <param name="passedParams">Переданные параметры.</param>
        /// <returns>Результат подстановки параметров - измененное тело макроса.</returns>
        private List<CodeEntity> ProcessMacroParams(Macro macro, IEnumerable<string> passedParams)
        {
            // First key param index in the params list
            int firstKeyParamIndex = -1;


            if (passedParams.Count() != macro.Parameters.Count)
            {
                throw new CustomException(string.Format(
                    ProcessorErrorMessages.IncorrectMacroCallParametersCount, macro.Name, passedParams.Count(), macro.Parameters.Count));
            }

            var first = passedParams.FirstOrDefault(x => x.Contains("="));
            if (first != null)
            {
                firstKeyParamIndex = Array.IndexOf(passedParams.ToArray(), first);
                if (passedParams.Any(x => !x.Contains("=") && Array.IndexOf(passedParams.ToArray(), x) > firstKeyParamIndex))
                {
                    throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroCallParameters, macro.Name));
                }
            }
            if (firstKeyParamIndex != macro.FirstKeyParameterIdx)
            {
                throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroCallParameters, macro.Name));
            }


            // формируем локальную область видимости (параметры в виде key-value)
            Dictionary<string, int> dict = new Dictionary<string, int>();

            // Delegate to process positioned params
            Action ParsePositionedParams = delegate ()
            {
                // If there is no key params, get all params as positioned
                int localFirstParamIndex = firstKeyParamIndex >= 0 ? firstKeyParamIndex : passedParams.Count();
                for (int i = 0; i < localFirstParamIndex; i++)
                {
                    string currentParam = passedParams.ToArray()[i];
                    var variable = VariablesStorage.Entities.FirstOrDefault(e => e.Name.EqualsIgnoreCase(currentParam));
                    if (variable == null && !int.TryParse(currentParam, out int temp))
                    {
                        throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroParameterValue, currentParam));
                    }
                    if (variable != null && variable.Value == null)
                    {
                        throw new CustomException(string.Format(ProcessorErrorMessages.MacroParameterIsEmptyVariable, currentParam));
                    }

                    int curerntParamValue = variable?.Value ?? int.Parse(currentParam);
                    dict.Add(macro.Parameters[i].Name, curerntParamValue);
                }
            };

            // Delegate to process key params
            Action ParseKeyParams = delegate ()
            {
                // If there is no key params - exit
                if (firstKeyParamIndex < 0) return;

                for (int i = firstKeyParamIndex; i < passedParams.Count(); i++)
                {
                    string currentParameter = passedParams.ToArray()[i];
                    string[] vals = currentParameter.Split('=');
                    if (vals.Length != 2)
                    {
                        throw new CustomException(string.Format(ProcessorErrorMessages.IcorrectMacroCallKeyParameter, macro.Name, currentParameter));
                    }

                    var macroParameter = macro.Parameters.FirstOrDefault(e => e.Name == vals[0]);
                    if (macroParameter == null)
                    {
                        throw new CustomException(string.Format(ProcessorErrorMessages.ParameterDoesNotExists, vals[0]));
                    }
                    if (macroParameter.Type != MacroParameterTypes.Key)
                    {
                        throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroParameterType, vals[0]));
                    }

                    var passedValue = vals[1];
                    int value = 0;
                    if (string.IsNullOrEmpty(passedValue))
                    {
                        // значение не указали
                        value = macroParameter.DefaultValue ??
                            throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroParameterValue, currentParameter));
                    }
                    else
                    {
                        var variable = VariablesStorage.Entities.FirstOrDefault(e => e.Name.EqualsIgnoreCase(vals[1]));
                        if (variable == null && !int.TryParse(vals[1], out int temp))
                        {
                            throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectMacroParameterValue, currentParameter));
                        }
                        if (variable != null && variable.Value == null)
                        {
                            throw new CustomException(string.Format(ProcessorErrorMessages.MacroParameterIsEmptyVariable, currentParameter));
                        }
                        value = variable?.Value ?? int.Parse(vals[1]);
                    }

                    if (dict.Keys.Contains(vals[0]))
                    {
                        throw new CustomException(string.Format(ProcessorErrorMessages.DublicateMacroCallParameter, vals[0]));
                    }

                    dict.Add(vals[0], value);
                }
            };

            ParsePositionedParams();
            ParseKeyParams();

            var processedBody = macro.Body.Select(e => (CodeEntity)e.Clone()).ToList();

            // замена параметров в макросе на числа
            var macroCount = 0;
            foreach (var sourceLine in processedBody)
            {
                if (sourceLine.Operation == "MACRO") macroCount++;
                if (sourceLine.Operation == "MEND") macroCount--;
                if (macroCount != 0) continue;

                if (sourceLine.Operation.EqualsIgnoreCase("WHILE"))
                {
                    if (sourceLine.Operands.Count > 0)
                    {
                        foreach (var sign in Helpers.ComparisonSigns)
                        {
                            var t = sourceLine.Operands[0].Split(new string[] { sign }, StringSplitOptions.None);
                            if (t.Length == 2)
                            {
                                if (macro.Parameters.Any(e => e.Name == t[0].Trim()))
                                {
                                    t[0] = dict[t[0].Trim()].ToString();
                                }
                                if (macro.Parameters.Any(e => e.Name == t[1].Trim()))
                                {
                                    t[1] = dict[t[1].Trim()].ToString();
                                }
                                sourceLine.Operands[0] = t[0] + sign + t[1];
                                break;
                            }
                        }
                    }
                    else
                    {
                        throw new CustomException(string.Format(ProcessorErrorMessages.IncorrectDirectiveUsage, sourceLine.Operation));
                    }
                }
                else if (sourceLine.Operation.In("SET", "INC"))
                {
                    // Ничего не делаем с операциями SET, INC - они умеют работать только с переменными
                    continue;
                }
                else
                {
                    for (int i = 0; i < sourceLine.Operands.Count; i++)
                    {
                        var currentOperand = sourceLine.Operands[i];
                        if (dict.Keys.Contains(currentOperand))
                        {
                            sourceLine.Operands[i] = dict[currentOperand].ToString();
                        }

                        if (currentOperand.Contains("="))
                        {
                            string[] t = currentOperand.Split(new string[] { "=" }, StringSplitOptions.None);
                            if (t.Length == 2)
                            {
                                if (macro.Parameters.Any(e => e.Name == t[1].Trim()))
                                {
                                    t[1] = dict[t[1].Trim()].ToString();
                                }
                                sourceLine.Operands[i] = t[0] + "=" + t[1];
                            }
                        }
                    }
                }
            }

            return processedBody;
        }
    }


    public static class CheckSourceEntity
    {
        /// <summary>
        /// Проверка на метку (может быть пустая или много двоеточий)
        /// </summary>
        /// <param name="se">строка с операцией меткой</param>
        public static void CheckLabel(CodeEntity se)
        {
            if (se.SourceString.Split(':').Length > 2 && se.Operation != "BYTE")
            {
                throw new CustomException($"{ProcessorErrorMessages.ExtraColonInLine} ({se.SourceString})");
            }
            if (se.SourceString.Split(':').Length > 1 && string.IsNullOrEmpty(se.SourceString.Split(':')[0]))
            {
                throw new CustomException($"{ProcessorErrorMessages.ExtraColonInLine} ({se.SourceString})");
            }
        }

        /// <summary>
        /// Проверка строки с операцией MACRO
        /// </summary>
        /// <param name="se">строка с операцией MACRO</param>
        public static void CheckMacro(CodeEntity se, int macroCount)
        {
            if (se.SourceString.Contains(":"))
            {
                throw new CustomException($"{ProcessorErrorMessages.LabesInMacroDefinition} ({se.SourceString})");
            }
            if (string.IsNullOrEmpty(se.Label) || !Helpers.IsLabel(se.Label))
            {
                throw new CustomException($"{ProcessorErrorMessages.IncorrectMacroName} ({se.SourceString})");
            }
            if (MacrosStorage.IsInTMO(se.Label))
            {
                throw new CustomException($"{ProcessorErrorMessages.MacrosAleradyExists} ({se.Label}): {se.SourceString}");
            }
            Helpers.CheckNames(se.Label);
        }

        /// <summary>
        /// Проверка строки с операцией MEND
        /// </summary>
        /// <param name="se">строка с операцией MEND</param>
        public static void CheckMend(CodeEntity se, int macroCount)
        {
            if (se.Operands.Count != 0)
            {
                throw new CustomException($"{ProcessorErrorMessages.MendWithParameters} ({se.SourceString})");
            }
            if (!string.IsNullOrEmpty(se.Label))
            {
                throw new CustomException($"{ProcessorErrorMessages.MendWithLabel} ({se.SourceString})");
            }
            if (macroCount == 0)
            {
                throw new CustomException($"{ProcessorErrorMessages.IncorrectMacroMendCount} ({se.SourceString})");
            }
        }

        /// <summary>
        /// Проверка строки с операцией END
        /// </summary>
        /// <param name="se">строка с операцией END</param>
        public static void CheckEnd(CodeEntity se, int macroCount)
        {
            if (macroCount != 0)
            {
                throw new CustomException($"{ProcessorErrorMessages.IncorrectMacroMendCount} ({se.SourceString})");
            }
        }

        /// <summary>
        /// Проверка макроподстановки
        /// </summary>
        public static void CheckMacroSubstitution(CodeEntity se, Macro macro)
        {
            if (se.Operands.Count != macro.Parameters.Count)
            {
                // Вставка параметров с дефолтным значением
                macro.Parameters
                  .Where(x => x.DefaultValue.HasValue && se.Operands.All(y => !y.Contains(x.Name.ToUpper())))
                  .ToList()
                  .ForEach(x => se.Operands.Add(x.Name.ToUpper() + "=" + x.DefaultValue.Value));

                if (se.Operands.Count != macro.Parameters.Count)
                {
                    throw new CustomException("Некорректное количество параметров. Введено: " + se.Operands.Count + ". Ожидается: " + macro.Parameters.Count);
                }
            }
            if (!string.IsNullOrEmpty(se.Label))
            {
                throw new CustomException("При макровызове макроса не должно быть меток: " + se.SourceString);
            }
        }

        /// <summary>
        /// Проверка строки с операцией Directives.Variable
        /// </summary>
        /// <param name="se">строка с операцией Directives.Variable</param>
        public static void CheckVariable(CodeEntity se)
        {
            if (se.Operands.Count > 0 && VariablesStorage.IsInVariablesStorage(se.Operands[0]))
            {
                throw new CustomException($"{ProcessorErrorMessages.VariableAleradyExists} ({se.SourceString})");
            }
            if (!String.IsNullOrEmpty(se.Label))
            {
                throw new CustomException($"{ProcessorErrorMessages.VariableDefinitionWithLabel} ({se.SourceString})");
            }
            if (se.Operands.Count == 2)
            {
                if (!Helpers.IsLabel(se.Operands[0]))
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectVariableName} ({se.SourceString})");
                }
                int temp;
                if (Int32.TryParse(se.Operands[1], out temp) == false)
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectVariableValue} ({se.SourceString})");
                }
            }
            else if (se.Operands.Count == 1)
            {
                if (!Helpers.IsLabel(se.Operands[0]))
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectVariableName} ({se.SourceString})");
                }
            }
            else
            {
                throw new CustomException($"{ProcessorErrorMessages.VariableIncorrectOperandsCount} ({se.SourceString})");
            }
            Helpers.CheckNames(se.Operands[0]);
        }

        /// <summary>
        /// Проверка строки с операцией SET
        /// </summary>
        /// <param name="se">строка с операцией SET</param>
        public static void CheckSet(CodeEntity se, Macro te)
        {
            if (!string.IsNullOrEmpty(se.Label))
            {
                throw new CustomException($"{ProcessorErrorMessages.SetWithLabel} ({se.SourceString})");
            }
            if (se.Operands.Count == 2)
            {
                if (!VariablesStorage.IsInVariablesStorage(se.Operands[0]))
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectVariableName} ({se.SourceString})");
                }
                int temp;
                if (int.TryParse(se.Operands[1], out temp) == false)
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectVariableValue} ({se.SourceString})");
                }
                foreach (Dictionary<List<string>, Macro> dict in VariablesStorage.WhileVar)
                {
                    if (dict.Keys.First().Contains(se.Operands[0]) && dict.Values.First() != te)
                    {
                        throw new CustomException($"{ProcessorErrorMessages.VariableIsLoopCounter} (Переменная: {se.Operands[0]}): {se.SourceString}");
                    }
                }
            }
            else
            {
                throw new CustomException($"{ProcessorErrorMessages.SetIncorrectOperands} ({se.SourceString})");
            }
        }

        /// <summary>
        /// Проверка строки с операцией INC
        /// </summary>
        /// <param name="se">строка с операцией INC</param>
        public static void CheckInc(CodeEntity se, Macro te)
        {
            if (!string.IsNullOrEmpty(se.Label))
            {
                throw new CustomException($"{ProcessorErrorMessages.IncWithLabel} ({se.SourceString})");
            }
            if (se.Operands.Count != 1)
            {
                throw new CustomException($"{ProcessorErrorMessages.IncIncorrectOperandsCount} ({se.SourceString})");
            }
            if (!VariablesStorage.IsInVariablesStorage(se.Operands[0]))
            {
                throw new CustomException($"{ProcessorErrorMessages.IncorrectVariableName} ({se.SourceString})");
            }
            if (VariablesStorage.Find(se.Operands[0]).Value == null)
            {
                throw new CustomException($"{ProcessorErrorMessages.NullVariable} ({se.Operands[0]})");
            }
            foreach (Dictionary<List<string>, Macro> dict in VariablesStorage.WhileVar)
            {
                if (dict.Keys.First().Contains(se.Operands[0]) && dict.Values.First() != te)
                {
                    throw new CustomException($"{ProcessorErrorMessages.VariableIsLoopCounter} (Переменная: {se.Operands[0]}) {se.SourceString}");
                }
            }
        }

    }

    public static class CheckBody
    {
        /// <summary>
        /// Проверка макроподстановки
        /// </summary>
        public static void CheckMacroRun(CodeEntity se, Macro parent, Macro child)
        {
            Macro current = parent;
            List<Macro> list = new List<Macro>();
            while (current.PreviousMacros != null)
            {
                if (list.Contains(current))
                {
                    throw new CustomException(ProcessorErrorMessages.Recursion);
                }
                list.Add(current);
                current = current.PreviousMacros;
            }
            if (MacrosStorage.IsInTMO(child.Name) && parent.Name == child.Name)
            {
                throw new CustomException($"{ProcessorErrorMessages.SelfMacroCall} (Макрос: {child.Name})");
            }
            if (MacrosStorage.IsInTMO(child.Name) && !parent.LocalTmo.Contains(child))
            {
                throw new CustomException($"{ProcessorErrorMessages.MacroScopeLocal} (Макрос: {child.Name})");
            }
        }
    }
}
