using System;
using System.Collections.Generic;
using System.Linq;

namespace SystemSoftware.MacroProcessor
{
    /// <summary>
    /// Объект, представляющий строку исходного кода.
    /// </summary>
    public class CodeEntity : ICloneable
    {
        /// <summary>
        /// Строка как string, как в исходниках.
        /// </summary>
        public string SourceString { get; set; }

        /// <summary>
        /// Родитель для исходных строк.
        /// </summary>
        public Processor Sources { get; set; }

        /// <summary>
        /// Метка.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Операция.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Операнды.
        /// </summary>
        public List<string> Operands { get; set; } = new List<string>();

        /// <summary>
        /// Подозрительна ли строка на макровызов.
        /// </summary>
        public bool IsRemove { get; set; }

        /// <summary>
        /// Номер вызова, чтобы знать, что заменять при макровызове.
        /// </summary>
        public int CallNumber { get; set; }

        /// <summary>
        /// Представление объекта в виде строки.
        /// </summary>
        /// <returns>Строка, представляющая текущий объект.</returns>
        public override string ToString()
        {
            string temp = "";
            if (!string.IsNullOrEmpty(Label) && !Label.Contains('%'))
            {
                temp += Label + ": ";
            }
            else if (!string.IsNullOrEmpty(Label) && Label.Contains('%'))
            {
                temp += Label + " ";
            }

            temp += Operation;

            foreach (string op in Operands)
            {
                temp += " " + op;
            }
            return temp;
        }

        /// <summary>
        /// Клонировать объект.
        /// </summary>
        /// <returns>Клон объекта.</returns>
        public object Clone()
        {
            var clone = (CodeEntity)this.MemberwiseClone();
            clone.Operands = new List<string>(Operands);
            return clone;
        }
    }
}
