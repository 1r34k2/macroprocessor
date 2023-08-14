using System;
using System.Collections.Generic;
using System.Linq;
using SystemSoftware.Common;
using SystemSoftware.Resources;

namespace SystemSoftware.MacroProcessor
{
    /// <summary>
    /// Класс, описывающий макрос.
    /// </summary>
    public class Macro
    {
        /// <summary>
        /// Имя макроса.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Тело макроса.
        /// </summary>
        public List<CodeEntity> Body { get; set; } = new List<CodeEntity>();

        /// <summary>
        /// Параметры.
        /// </summary>
        public List<MacroParameter> Parameters { get; set; }

        /// <summary>
        /// Первый ключевой параметр.
        /// </summary>
        public int FirstKeyParameterIdx =>
            Array.IndexOf(Parameters?.ToArray() ?? new MacroParameter[] { }, Parameters?.FirstOrDefault(e => e.Type == MacroParameterTypes.Key));


        /// <summary>
        /// При обработке вложенных макроописаний нобходимы макрос-родитель и макросы-дети.
        /// </summary>
        public Macro ParentMacros { get; set; }

        /// <summary>
        /// При обработке вложенных макроописаний необходимы макросы-дети.
        /// </summary>
        public List<Macro> ChildrenMacros { get; set; } = new List<Macro>();

        /// <summary>
        /// Локальная область видимости макросов.
        /// </summary>
        public List<Macro> LocalTmo { get; set; } = new List<Macro>();

        /// <summary>
        /// При обработке перекрестных ссылок необходим параметр, определяющий макрос, из которого бы вызван данный макрос.
        /// </summary>
        public Macro PreviousMacros { get; set; }

        /// <summary>
        /// Является ли макрос корневым - телом основной программы.
        /// </summary>
        public bool IsRootMacro { get; set; }

        /// <summary>
        /// Количество итераций для того, чтобы считать цикл бесконечным.
        /// </summary>
        public const int INFINITE_LOOP_COUNT = 50000;

        /// <summary>
        /// Для уникальных меток (Метки внутри макроса - да).
        /// </summary>
        private int _uniqueLabelCounter { get; set; }

        /// <summary>
        /// Количество вызовов этого макроса.
        /// </summary>
        private int _counter { get; set; }

        public static bool lastWasAifAgo = false;

        /// <summary>
        /// Макрогенерация.
        /// </summary>
        /// <param name="source">Тело макроса.</param>
        /// <returns>Результирующий ассемблерный код.</returns>
        public Processor CallMacro(List<CodeEntity> source)
        {
            Processor macroSourceCode = new Processor(source);

            // проверки
            CheckMacros.CheckMacroLabels(this);
            CheckMacros.CheckLocalTmo();

            #region Unique macro labels

            //заменяем метки на "крутые" уникальные метки (Метки внутри макроса - да)
            List<string> localMacroLabels = new List<string>();
            int macroCount = 0;
            foreach (CodeEntity currentLine in macroSourceCode.SourceCodeLines)
            {
                if (currentLine.Operation == "MACRO") macroCount++;
                if (currentLine.Operation == "MEND") macroCount--;
                if (macroCount != 0) continue;

                if (!string.IsNullOrEmpty(currentLine.Label))
                {
                    var str = currentLine.Label;
                    if (currentLine.Label.IndexOf("%") == 0)
                        str = currentLine.Label.Remove(0, 1);

                    if (currentLine.Label.Contains("%") && currentLine.Label.Contains(":"))
                        throw new CustomException("Невозможно определить тип метки");

                    if (!Helpers.IsLabel(str))
                    {
                        throw new CustomException($"{ProcessorErrorMessages.IncorrectLabelInMacro} (Метка {currentLine.Label}, макрос {currentLine.SourceString})");
                        throw new CustomException($"{ProcessorErrorMessages.DuplicateLabelInMacro} (Метка {currentLine.Label}, макрос {currentLine.SourceString})");
                    }
                    Helpers.CheckNames(currentLine.Label);
                    if (localMacroLabels.Contains(currentLine.Label))
                    {
                        throw new CustomException($"{ProcessorErrorMessages.DuplicateLabelInMacro} (Метка {currentLine.Label}, макрос {currentLine.SourceString})");
                    }
                    
                    localMacroLabels.Add(currentLine.Label);
                }
            }

            macroCount = 0;
            // Список меток, которые уже найдены
            for (int i = 0; i < macroSourceCode.SourceCodeLines.Count; i++)
            {
                CodeEntity currentLine = macroSourceCode.SourceCodeLines[i];
                // Формирование уникальных меток
                if (currentLine.Operation == "MACRO")
                {
                    macroCount++;
                }
                else if (currentLine.Operation == "MEND")
                {
                    macroCount--;
                }
                if (macroCount == 0)
                {
                    if (currentLine.Label != null && currentLine.Label.Contains("%"))
                        continue;
                    if (!string.IsNullOrEmpty(currentLine.Label))
                    {
                        currentLine.Label = currentLine.Label + "_" + Name + "_" + _uniqueLabelCounter;
                    }
                    for (int j = 0; j < currentLine.Operands.Count; j++)
                    {
                        if (currentLine.Operands[j].Contains("%"))
                            continue;
                        if (localMacroLabels.Contains(currentLine.Operands[j]) )
                        {
                            currentLine.Operands[j] = currentLine.Operands[j] + "_" + Name + "_" + _uniqueLabelCounter;
                        }
                    }
                }
            }

            _uniqueLabelCounter++;

            #endregion

            #region while, if...

            // исполнять ли команду дальше
            Stack<bool> runStack = new Stack<bool>();
            runStack.Push(true);
            // стек комманд, появлявшихся ранее
            Stack<ConditionalDirective> commandStack = new Stack<ConditionalDirective>();
            // стек строк, куда надо вернуться при while
            Stack<int> whileStack = new Stack<int>();

            List<CodeEntity> macros = new List<CodeEntity>();

            macroCount = 0;
            for (int i = 0; i < macroSourceCode.SourceCodeLines.Count; i++)
            {
                CodeEntity current = macroSourceCode.SourceCodeLines[i].Clone() as CodeEntity;

                if (lastWasAifAgo)
                {
                    macros.RemoveAt(macros.Count - 1);
                    lastWasAifAgo = false;

                }
                macros.Add(current);

                _counter++;
                if (_counter == INFINITE_LOOP_COUNT)
                {
                    throw new CustomException(ProcessorErrorMessages.InfiniteLoop);
                }

                // Вложенные макросы не обрабатываем
                if (current.Operation == "MACRO")
                {
                    macroCount++;
                }
                else if (current.Operation == "MEND")
                {
                    macroCount--;
                }
                if (macroCount == 0)
                {
                    CheckMacros.CheckInner(current, commandStack);
                    if (current.Operation == "IF")
                    {
                        CheckMacros.CheckIf(this);
                        commandStack.Push(ConditionalDirective.IF);
                        runStack.Push(runStack.Peek() && Helpers.Compare(current.Operands[0]));
                        continue;
                    }
                    if (current.Operation == "ELSE")
                    {
                        CheckMacros.CheckIf(this);
                        commandStack.Pop();
                        commandStack.Push(ConditionalDirective.ELSE);
                        bool elseFlag = runStack.Pop();
                        runStack.Push(runStack.Peek() && !elseFlag);
                        continue;
                    }
                    if (current.Operation == "ENDIF")
                    {
                        CheckMacros.CheckIf(this);
                        commandStack.Pop();
                        runStack.Pop();
                        continue;
                    }
                    if (current.Operation == "WHILE")
                    {
                        CheckMacros.CheckWhile(this);
                        if (current.Operands.Count == 1)
                            Helpers.PushConditionArgs(current.Operands[0], this);
                        commandStack.Push(ConditionalDirective.ENDIF);
                        runStack.Push(runStack.Peek() && Helpers.Compare(current.Operands[0]));
                        whileStack.Push(i);
                        continue;
                    }
                    if (current.Operation == "ENDW")
                    {
                        CheckMacros.CheckWhile(this);
                        commandStack.Pop();
                        int newI = whileStack.Pop() - 1;
                        if (runStack.Pop())
                        {
                            i = newI;
                        }
                        continue;
                    }

                    if (current.Operation.In("AIF", "AGO"))
                    {
                        if (current.Operation == "AIF" && current.Operands.Count != 2)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveOperands} {Directives.Aif}");
                        }
                        // If AIF condition is false, skip current line
                        if (current.Operation == "AIF" && !Helpers.Compare(current.Operands[0])) 
                        {
                            int a;
                            if (whileStack.Any())
                                lastWasAifAgo = true;
                            continue; 
                        }

                        if (runStack.Peek())
                        {
                            CheckMacros.CheckAif(macros, this);

                            if (!(current.Operation == "AIF" && current.Operands[1].IndexOf("%") == 0 ) && !(current.Operation == "AGO" && current.Operands[0].IndexOf('%') == 0))
                                throw new CustomException("Неверное использование директивы AIF/AGO");

                            string label = current.Operation == "AIF" ? current.Operands[1] : current.Operands[0];
                            // находим метку, чтобы туда прыгнуть
                            bool ready = false;
                            Stack<bool> agoStack = new Stack<bool>();

                            // вверх
                            int localMacroCount = 0;
                            for (int j = i; j >= 0; j--)
                            {
                                // Вложенные макросы не смотрим
                                if (macroSourceCode.SourceCodeLines[j].Operation == "MACRO") localMacroCount++;
                                if (macroSourceCode.SourceCodeLines[j].Operation == "MEND") localMacroCount--;
                                if (localMacroCount != 0) continue;

                                if (macroSourceCode.SourceCodeLines[j].Operation?.In("WHILE", "IF") == true)
                                {
                                    if (agoStack.Count > 0)
                                    {
                                        agoStack.Pop();
                                    }
                                }
                                if (macroSourceCode.SourceCodeLines[j].Operation == "ELSE")
                                {
                                    if (agoStack.Count > 0)
                                    {
                                        agoStack.Pop();
                                    }
                                    agoStack.Push(false);
                                }
                                if (macroSourceCode.SourceCodeLines[j].Operation?.In("ENDIF", "ENDW") == true)
                                {
                                    agoStack.Push(false);
                                }
                                if (macroSourceCode.SourceCodeLines[j].Label == label && (agoStack.Count == 0 || agoStack.Peek()))
                                {
                                    i = j - 1;
                                    ready = true;
                                    break;
                                }
                            }

                            // вниз
                            if (!ready)
                            {
                                localMacroCount = 0;
                                for (int j = i; j < macroSourceCode.SourceCodeLines.Count; j++)
                                {
                                    // Вложенные макросы не смотрим
                                    if (macroSourceCode.SourceCodeLines[j].Operation == "MACRO") localMacroCount++;
                                    if (macroSourceCode.SourceCodeLines[j].Operation == "MEND") localMacroCount--;
                                    if (localMacroCount != 0) continue;

                                    if (macroSourceCode.SourceCodeLines[j].Operation?.In("WHILE", "IF") == true)
                                    {
                                        agoStack.Push(false);
                                    }
                                    if (macroSourceCode.SourceCodeLines[j].Operation == "ELSE")
                                    {
                                        if (agoStack.Count > 0)
                                        {
                                            agoStack.Pop();
                                        }
                                        agoStack.Push(false);
                                    }
                                    if (macroSourceCode.SourceCodeLines[j].Operation?.In("ENDIF", "ENDW") == true)
                                    {
                                        if (agoStack.Count > 0)
                                        {
                                            agoStack.Pop();
                                        }
                                    }
                                    if (macroSourceCode.SourceCodeLines[j].Label == label && (agoStack.Count == 0 || agoStack.Peek()))
                                    {
                                        i = j - 1;
                                        ready = true;
                                        break;
                                    }
                                }
                            }
                            if (!ready)
                            {
                                throw new CustomException("Метка " + label + " при директиве " + current.Operation +
                                    " находится вне зоны видимости или не описана");
                            }
                        }
                        continue;
                    }
                }
                if (runStack.Peek())
                {
                    macroSourceCode.FirstRunStep(current, this);
                }
            }

            #endregion

            // Список меток, которые уже найдены
            List<string> markedLabels = new List<string>();
            macroCount = 0;
            foreach (CodeEntity se in macroSourceCode.AssemblerCode)
            {
                if (se.Operation == "MACRO") macroCount++;
                if (se.Operation == "MEND") macroCount--;
                if (macroCount != 0) continue;

                if (!string.IsNullOrEmpty(se.Label))
                {
                    if (markedLabels.Contains(se.Label))
                    {
                        throw new CustomException($"{ProcessorErrorMessages.DuplicateLabelInMacro} (Метка {se.Label}, макрос {Name})");
                    }
                    markedLabels.Add(se.Label);
                }
            }

            return macroSourceCode;
        }

        
    }


    public static class CheckMacros
    {

        public static void CheckAif(List<CodeEntity> te, Macro mc)
        {
            // Вложенные макросы не обрабатываем
            List<CodeEntity> result = new List<CodeEntity>();
            int macroCount = 0;
            foreach (CodeEntity se in te)
            {
                if (se.Operation == "MACRO")
                {
                    macroCount++;
                }
                else if (se.Operation == "MEND")
                {
                    macroCount--;
                }
                if (macroCount == 0)
                {
                    result.Add((CodeEntity)se.Clone());
                }
            }

            try
            {
                foreach (CodeEntity str in result)
                {
                    if (str.Operation == "AIF")
                    {
                        if (str.Operands.Count != 2)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveOperands} {Directives.Aif}");
                        }
                        if (!string.IsNullOrEmpty(str.Label))
                        {
                            throw new CustomException($"{ProcessorErrorMessages.DirectiveWithLabel_1} {Directives.Aif} {ProcessorErrorMessages.DirectiveWithLabel_2}");
                        }
                        for (int i = 0; i < Processor.assemblyMarks.Count; i++)
                        {
                            if (Processor.assemblyMarks[i].Name == str.Operands[1] && mc.Name == Processor.assemblyMarks[i].Macro)
                            {
                                if (Processor.assemblyMarks[i].Used == false)
                                {
                                    Processor.assemblyMarks[i].Used = true;
                                    Macro.lastWasAifAgo = true;
                                }
                                else
                                    throw new CustomException("Найдено зацикливание или повторное обращение к метке");
                            }
                        }
                    }
                    if (str.Operation == "AGO")
                    {
                        if (str.Operands.Count != 1)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.Ago}");
                        }
                        if (!string.IsNullOrEmpty(str.Label))
                        {
                            throw new CustomException($"{ProcessorErrorMessages.DirectiveWithLabel_1} {Directives.Ago} {ProcessorErrorMessages.DirectiveWithLabel_2}");
                        }
                        for (int i = 0; i < Processor.assemblyMarks.Count; i++)
                        {
                            if (Processor.assemblyMarks[i].Name == str.Operands[0] && mc.Name == Processor.assemblyMarks[i].Macro)
                            {
                                if (Processor.assemblyMarks[i].Used == false)
                                {
                                    Processor.assemblyMarks[i].Used = true;
                                    Macro.lastWasAifAgo = true;
                                }
                                else
                                    throw new CustomException("Найдено зацикливание или повторное обращение к метке");
                            }
                        }
                    }
                }
            }
            catch (CustomException ex)
            {
                throw new CustomException(ex.Message);
            }
            catch (Exception)
            {
                throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.Aif}-{Directives.Ago}");
            }
        }
        /// <summary> Проверяет макрос на наличие меток
        /// </summary>
        public static void CheckMacroLabels(Macro te)
        {
            var result = new List<CodeEntity>();

            // Вложенные макросы не обрабатываем
            int macroCount = 0;
            foreach (CodeEntity se in te.Body)
            {
                if (se.Operation == "MACRO")
                {
                    macroCount++;
                }
                else if (se.Operation == "MEND")
                {
                    macroCount--;
                }
                if (macroCount == 0)
                {
                    result.Add(se.Clone() as CodeEntity);
                }
            }

            // Список меток, которые уже найдены
            var markedLabels = new List<string>();
            foreach (var sourceLine in result)
            {
                if (string.IsNullOrEmpty(sourceLine.Label) || sourceLine.Operation.ToUpper() == "MACRO")
                {
                    continue;
                }

                if (markedLabels.Contains(sourceLine.Label))
                {
                    throw new CustomException($"{ProcessorErrorMessages.DuplicateLabelInMacro} (Метка {sourceLine.Label}, макрос {te.Name})");
                }
                markedLabels.Add(sourceLine.Label);
            }
        }

        /// <summary>
        /// Локальная область видимости макросов. Из parent можно вызвать только макросы localTMO
        /// </summary>
        public static void CheckLocalTmo()
        {
            foreach (Macro te in MacrosStorage.Entities)
            {
                te.LocalTmo.Clear();
                Macro current = te;
                while (current != MacrosStorage.Root)
                {
                    te.LocalTmo.AddRange(current.ChildrenMacros);
                    current = current.ParentMacros;
                }
                te.LocalTmo.AddRange(current.ChildrenMacros);
                te.LocalTmo.Remove(te);
            }
            MacrosStorage.Root.LocalTmo = MacrosStorage.Root.ChildrenMacros;
        }

        /// <summary>
        /// Проверка макроса на WHILE-ENDW
        /// </summary>
        public static void CheckWhile(Macro te)
        {
            int whileCount = 0;

            // Вложенные макросы не обрабатываем
            List<CodeEntity> result = new List<CodeEntity>();
            int macroCount = 0;
            foreach (CodeEntity se in te.Body)
            {
                if (se.Operation == "MACRO")
                {
                    macroCount++;
                }
                else if (se.Operation == "MEND")
                {
                    macroCount--;
                }
                if (macroCount == 0)
                {
                    result.Add(se.Clone() as CodeEntity);
                }
            }

            //проверка корректности WHILE-ENDW
            try
            {
                foreach (CodeEntity str in result)
                {
                    if (str.Operation == "WHILE")
                    {
                        if (str.Operands.Count != 1)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveOperands} {Directives.While} ({str.SourceString})");
                        }
                        if (!string.IsNullOrEmpty(str.Label))
                        {
                            throw new CustomException($"{ProcessorErrorMessages.DirectiveWithLabel_1} {Directives.While} {ProcessorErrorMessages.DirectiveWithLabel_2} ({str.SourceString})");
                        }
                        whileCount++;
                    }
                    else if (str.Operation == "ENDW")
                    {
                        if (str.Operands.Count != 0)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveOperands} {Directives.Endw} ({str.SourceString})");
                        }
                        if (!string.IsNullOrEmpty(str.Label))
                        {
                            throw new CustomException($"{ProcessorErrorMessages.DirectiveWithLabel_1} {Directives.Endw} {ProcessorErrorMessages.DirectiveWithLabel_2} ({str.SourceString})");
                        }
                        whileCount--;
                        if (whileCount < 0)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.While}-{Directives.Endw}");
                        }
                    }
                    else if ((str.Operation == "MACRO" || str.Operation == "MEND") && whileCount > 0)
                    {
                        throw new CustomException(ProcessorErrorMessages.MacroDefinitionInLoop);
                    }
                    else if (str.Operation == Directives.Variable && whileCount > 0)
                    {
                        throw new CustomException(ProcessorErrorMessages.VariablesInLoop);
                    }
                    else if (!string.IsNullOrEmpty(str.Label) && str.Operation != "MACRO" && whileCount > 0)
                    {
                        throw new CustomException(ProcessorErrorMessages.LabelsInLoop);
                    }
                }

                if (whileCount != 0)
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.While}-{Directives.Endw}");
                }
            }
            catch (CustomException ex)
            {
                throw new CustomException(ex.Message);
            }
            catch (Exception)
            {
                throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.While}-{Directives.Endw}");
            }
        }

        /// <summary>
        /// Проверка макроса на IF-ELSE-ENDIF
        /// </summary>
        /// <param name="body"></param>
        public static void CheckIf(Macro te)
        {
            Stack<bool> stackIfHasElse = new Stack<bool>();

            // Вложенные макросы не обрабатываем
            List<CodeEntity> result = new List<CodeEntity>();
            int macroCount = 0;
            foreach (CodeEntity se in te.Body)
            {
                if (se.Operation == "MACRO")
                {
                    macroCount++;
                }
                else if (se.Operation == "MEND")
                {
                    macroCount--;
                }
                if (macroCount == 0)
                {
                    result.Add(se.Clone() as CodeEntity);
                }
            }

            //проверка корректности IF-ELSE-ENDIF
            try
            {
                foreach (CodeEntity str in result)
                {
                    if (str.Operation == "IF")
                    {
                        if (str.Operands.Count != 1)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveOperands} {Directives.If} ({str.SourceString})");
                        }
                        if (!string.IsNullOrEmpty(str.Label))
                        {
                            throw new CustomException($"{ProcessorErrorMessages.DirectiveWithLabel_1} {Directives.If} {ProcessorErrorMessages.DirectiveWithLabel_2} ({str.SourceString})");
                        }
                        stackIfHasElse.Push(false);
                    }
                    if (str.Operation == "ELSE")
                    {
                        if (str.Operands.Count != 0)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveOperands} {Directives.Else} ({str.SourceString})");
                        }
                        if (!string.IsNullOrEmpty(str.Label))
                        {
                            throw new CustomException($"{ProcessorErrorMessages.DirectiveWithLabel_1} {Directives.Else} {ProcessorErrorMessages.DirectiveWithLabel_2} ({str.SourceString})");
                        }
                        if (stackIfHasElse.Peek() == true)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.ExtraBranch} {Directives.Else} ({str.SourceString})");
                        }
                        else
                        {
                            stackIfHasElse.Pop();
                            stackIfHasElse.Push(true);
                        }
                    }
                    if (str.Operation == "ENDIF")
                    {
                        if (str.Operands.Count != 0)
                        {
                            throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveOperands} {Directives.EndIf}");
                        }
                        if (!string.IsNullOrEmpty(str.Label))
                        {
                            throw new CustomException($"{ProcessorErrorMessages.DirectiveWithLabel_1} {Directives.EndIf} {ProcessorErrorMessages.DirectiveWithLabel_2} ({str.SourceString})");
                        }
                        stackIfHasElse.Pop();
                    }
                }

                if (stackIfHasElse.Count > 0)
                {
                    throw new CustomException($"{ProcessorErrorMessages.DirectiveMissed} {Directives.EndIf}");
                }
            }
            catch (CustomException ex)
            {
                throw new CustomException(ex.Message);
            }
            catch (Exception)
            {
                throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.If}-{Directives.EndIf}");
            }
        }

        /// <summary>
        /// Проверка вложенностей
        /// </summary>
        /// <param name="current"></param>
        /// <param name="stack"></param>
        public static void CheckInner(CodeEntity current, Stack<ConditionalDirective> stack)
        {
            if (current.Operation == "IF")
            {
                return;
            }
            if (current.Operation == "ELSE")
            {
                if (stack.Count > 0 && stack.Peek() != ConditionalDirective.IF)
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.Else}");
                }
                return;
            }
            if (current.Operation == "ENDIF")
            {
                if (stack.Count > 0 && stack.Peek() != ConditionalDirective.IF && stack.Peek() != ConditionalDirective.ELSE)
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.EndIf}");
                }
                return;
            }
            if (current.Operation == "WHILE")
            {
                return;
            }
            if (current.Operation == "ENDW")
            {
                if (stack.Count > 0 && stack.Peek() != ConditionalDirective.ENDIF)
                {
                    throw new CustomException($"{ProcessorErrorMessages.IncorrectDirectiveUsage} {Directives.Endw}");
                }
                return;
            }
        }
    }
}
