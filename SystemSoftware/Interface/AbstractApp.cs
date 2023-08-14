using System;
using System.Collections.Generic;
using SystemSoftware.Common;
using SystemSoftware.MacroProcessor;

namespace SystemSoftware.Interface
{
    public abstract class AbstractApp
    {
        /// <summary>
        /// Путь к файлу с исходным кодом.
        /// </summary>
        public static readonly string InitialInputFile = Helpers.CurrentDirectory + "\\input.txt";

        /// <summary>
        /// Путь к файлу с результирующим ассемблерным кодом.
        /// </summary>
        public static readonly string InitialOutputFile = Helpers.CurrentDirectory + "\\output.txt";

        /// <summary>
        /// Путь к файлу с исходным кодом.
        /// </summary>
        public virtual string InputFile { get; set; } = InitialInputFile;

        /// <summary>
        /// Путь к файлу с результирующим ассемблерным кодом.
        /// </summary>
        public virtual string OutputFile { get; set; } = InitialOutputFile;
        
        /// <summary>
        /// Текущий номер строки исходного кода.
        /// </summary>
        public int SourceCodeIndex { get; set; }

        /// <summary>
        /// Исходный код программы.
        /// </summary>
        public Processor SourceCode { get; set; }

        /// <summary>
        /// Список строк с исходным кодом программы.
        /// </summary>
        public List<string> SourceStrings { get; set; }

        /// <summary>
        /// Состояние программы в текущий момент (1/2 проход).
        /// </summary>
        [Obsolete("Всегда 1 проход")]
        public RunMode RunMode { get; } = RunMode.FirstRun;
    }
}
