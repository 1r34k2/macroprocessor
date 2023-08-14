using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SystemSoftware.Common;
using SystemSoftware.MacroProcessor;

namespace SystemSoftware.Interface
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Объект визуального приложения, которое будет реализовывать логику макропроцессора.
        /// </summary>
        private VisualApp _program;
        private bool prepareFlag = false;
        public MainForm()
        {
            InitializeComponent();
            Show();
            Activate();

            // Инициализация GUI приложения 
            /*var sourceCodeText = tbSourceCode.Text.Split('\n');
            _program = new VisualApp(sourceCodeText);*/
        }

        /// <summary>
        /// Следующий шаг выполнения проги
        /// </summary>
        private void OnNextStep(object sender, EventArgs e)
        {
            Action HandleError = delegate ()
            {
                SetButtonsDisabled();
                btnRefreshAll.Enabled = true;
                btnFirstRun.Enabled = false;
                _program.PrintTmo(dgvTmo);
                _program.PrintVariablesTable(dgvVariables);
                _program.PrintMacroNameTable(dgvMacroNames);
            };

            if (!prepareFlag)
            {
                var sourceCodeText = tbSourceCode.Text.Split('\n');
                _program = new VisualApp(sourceCodeText);
                prepareFlag = true;
            }

            try
            {
                //btnFirstRun.Enabled = false;
                btnRefreshAll.Enabled = true;

                // если исходный текст пуст
                if (_program.SourceCode.SourceCodeLines.Count == 0)
                {
                    throw new CustomException("Исходный текст должен содержать хотя бы одну строку");
                }
                if (_program.SourceCodeIndex == 0)
                {
                    tbError.Clear();
                }

                // Последний шаг программы
                var isLastStep = _program.SourceCodeIndex + 1 == _program.SourceCode.SourceCodeLines.Count
                    && _program.RunMode == RunMode.FirstRun;
                if (isLastStep)
                {
                    btnNextStep.Enabled = false;
                    CheckSourceEntity.CheckEnd(new CodeEntity(), 0);
                    btnFirstRun.Enabled = false;
                }

                // Собственно шаг
                _program.NextFirstStep(tbAssemblerCode);

                _program.PrintAssemblerCode(tbAssemblerCode);
                _program.PrintTmo(dgvTmo);
                _program.PrintVariablesTable(dgvVariables);
                _program.PrintMacroNameTable(dgvMacroNames);
            }
            catch (CustomException ex)
            {
                tbError.Text = ex.Message;
                HandleError();
            }
            catch (Exception)
            {
                tbError.Text = "Произошла ошибка при попытке совершить следующий шаг программы";
                HandleError();
            }
        }

        /// <summary>
        /// Первый проход
        /// </summary>
        private void OnFirstRun(object sender, EventArgs e)
        {
            btnFirstRun.Enabled = false;
            btnNextStep.Enabled = false;
            btnRefreshAll.Enabled = true;

            if(!prepareFlag)
            {
                var sourceCodeText = tbSourceCode.Text.Split('\n');
                _program = new VisualApp(sourceCodeText);
                prepareFlag = true;
            }

            while (true)
            {
                var isLastStep = _program.SourceCodeIndex == _program.SourceCode.SourceCodeLines.Count
                    && _program.RunMode == RunMode.FirstRun;
                // если ошибка или конец текста - не продолжаем
                if (!string.IsNullOrEmpty(tbError.Text) || isLastStep)
                {
                    break;
                }
                // иначе выполняем шаг
                OnNextStep(sender, e);
            }
        }

        /// <summary>
        /// При загрузке формы заплняем TB исходниками, если можно
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFormLoad(object sender, EventArgs e)
        {
            tbInputFile.Text = _program?.InputFile ?? VisualApp.InitialInputFile;
            tbOutputFile.Text = _program?.OutputFile ?? VisualApp.InitialOutputFile;

            SetButtonsDisabled();

            if (!string.IsNullOrEmpty(tbInputFile.Text))
            {
                try
                {
                    FillSourceTextBoxFromFile(tbInputFile.Text, tbSourceCode);
                    SetButtonsDisabled(false);
                    tbError.Text = string.Empty;
                }
                catch (Exception ex)
                {
                    tbError.Text = ex.Message;
                    SetButtonsDisabled();
                }
            }
        }

        /// <summary>
        /// Обновить все данные, заново начать прогу
        /// </summary>
        private void OnRefreshAll(object sender, EventArgs e)
        {
            prepareFlag = false;
            tbError.Clear();
            tbAssemblerCode.Clear();
            _program.PrintTmo(dgvTmo);
            _program.PrintVariablesTable(dgvVariables);
            _program.PrintMacroNameTable(dgvMacroNames);

            VariablesStorage.Refresh();
            MacrosStorage.Refresh();
            MacrosStorage.macros = new List<string>();
            Processor.assemblyMarks = new List<StructMarks>();

            SetButtonsDisabled(false);
        }

        /// <summary>
        /// Загрузить исходники из выбранного файла в TextBox
        /// </summary>
        private void OnLoadFromFile(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "Text Files (.txt)|*.txt";
                ofd.InitialDirectory = Helpers.CurrentDirectory;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    tbSourceCode.Clear();
                    tbInputFile.Text = ofd.FileName;

                    FillSourceTextBoxFromFile(ofd.FileName, tbSourceCode);
                }

                SetButtonsDisabled(false);
                tbError.Text = string.Empty;
            }
            catch (Exception ex)
            {
                tbError.Text = ex.Message;
                SetButtonsDisabled();
            }
        }

        /// <summary>
        /// Записать результат в файл
        /// </summary>
        private void OnSaveIntoFile(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text Files (.txt)|*.txt";
            sfd.InitialDirectory = Helpers.CurrentDirectory;
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                tbInputFile.Text = sfd.FileName;
                List<string> temp = tbAssemblerCode.Text.Split('\n').ToList();
                StreamWriter sw = new StreamWriter(sfd.FileName);
                foreach (string str in temp)
                {
                    sw.WriteLine(str);
                }
                sw.Close();
            }
        }

        /// <summary>
        /// Заполнить TеxtBox исходных данных, считав их из заданного файла
        /// </summary>
        /// <param name="file">Имя файла с исходниками</param>
        /// <param name="tb">TеxtBox исходных данных</param>
        public void FillSourceTextBoxFromFile(string file, TextBox tb)
        {
            try
            {
                var temp = string.Empty;
                StreamReader sr = new StreamReader(file);
                while ((temp = sr.ReadLine()) != null)
                {
                    tb.AppendText(temp + Environment.NewLine);
                }
                sr.Close();
            }
            catch
            {
                throw new CustomException("Не удалось загрузить данные с файла. Возможно путь к файлу указан неверно");
            }
        }

        /// <summary>
        /// Заблокировать/разблокировать кнопки.
        /// </summary>
        /// <param name="disabled">Флаг блокировки.</param>
        private void SetButtonsDisabled(bool disabled = true)
        {
            btnNextStep.Enabled = !disabled;
            btnRefreshAll.Enabled = !disabled;
            btnFirstRun.Enabled = !disabled;
        }

    }
}
