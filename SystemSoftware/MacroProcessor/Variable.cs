namespace SystemSoftware.MacroProcessor
{
    /// <summary>
    /// Переменная для макрогенерации.
    /// </summary>
    public class Variable
    {
        /// <summary>
        /// Название переменной.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Значение переменной.
        /// </summary>
        public int? Value { get; set; }

        public Variable(string name, int? value)
        {
            Name = name;
            Value = value;
        }
    }
}
