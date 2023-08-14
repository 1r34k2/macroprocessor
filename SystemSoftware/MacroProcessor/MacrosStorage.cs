using System.Collections.Generic;
using System.Linq;

namespace SystemSoftware.MacroProcessor
{
    /// <summary>
    /// Таблица макроопределений.
    /// </summary>
    public static class MacrosStorage
    {
        /// <summary>
        /// Список макроопределений.
        /// </summary>
        public static List<Macro> Entities { get; set; } = new List<Macro>();

        /// <summary>
        /// Корневой макрос - тело основной программы.
        /// </summary>
        public static Macro Root { get; } = new Macro() { ParentMacros = null, ChildrenMacros = new List<Macro>(), IsRootMacro = true };

        public static List<string> macros { get; set; } = new List<string>();

        /// <summary>
        /// Обновить ТМО.
        /// </summary>
        public static void Refresh()
        {
            Entities = new List<Macro>();
        }

        /// <summary>
        /// Поиск макроса в ТМО по имени.
        /// </summary>
        /// <param name="name">Имя макроса.</param>
        /// <returns>Найденный макрос или null.</returns>
        public static Macro SearchInTMO(string name)
        {
            return Entities.SingleOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// Есть ли макрос в ТМО.
        /// </summary>
        /// <param name="name">Имя макроса.</param>
        /// <returns>Флаг, есть ли макрос в ТМО.</returns>
        public static bool IsInTMO(string name)
        {
            return SearchInTMO(name) != null;
        }
    }
}
