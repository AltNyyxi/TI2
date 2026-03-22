using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace TI2
{
    public partial class MainWindow : Window
    {
        private byte[] inputBytes;
        private string selectedFilePath;

        public MainWindow()
        {
            InitializeComponent();
        }

        //x^29 + x^2 + 1
        private class LFSR
        {
            private int[] state;
            private const int StateSize = 29;  

            public LFSR(string seedStr)
            {
                state = new int[StateSize];
                for (int i = 0; i < StateSize && i < seedStr.Length; i++)
                {
                    state[i] = seedStr[i] == '1' ? 1 : 0;
                }
            }

            public int NextBit()
            {

                int bit29 = state[28];  
                int bit2 = state[1];   

                int feedback = bit29 ^ bit2;
                int outputBit = state[0];  

                for (int i = 0; i < StateSize - 1; i++)
                {
                    state[i] = state[i + 1];
                }
                state[StateSize - 1] = feedback;  

                return outputBit;
            }

            public byte NextByte()
            {
                byte result = 0;
                for (int i = 0; i < 8; i++)
                {
                    result = (byte)((result << 1) | NextBit());
                }
                return result;
            }
        }

        private void SeedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            string text = textBox.Text;

            string filteredText = new string(text.Where(c => c == '0' || c == '1').ToArray());

            if (text != filteredText)
            {
                int cursorPos = textBox.SelectionStart;
                textBox.Text = filteredText;
                textBox.SelectionStart = Math.Min(cursorPos, filteredText.Length);
            }

            CounterTextBlock.Text = $"Символов: {filteredText.Length}/29";

            if (filteredText.Length == 29)
            {
                CounterTextBlock.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                CounterTextBlock.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Выберите файл для обработки";

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                FilePathTextBox.Text = selectedFilePath;

                try
                {
                    inputBytes = File.ReadAllBytes(selectedFilePath);
                    MessageBox.Show($"Файл загружен. Размер: {inputBytes.Length} байт",
                                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при чтении файла: {ex.Message}", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            string seedStr = SeedTextBox.Text;

            if (seedStr.Length != 29)
            {
                MessageBox.Show("Введите ровно 29 бит (0 и 1)!", "Ошибка ввода",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (inputBytes == null || inputBytes.Length == 0)
            {
                MessageBox.Show("Выберите файл!", "Ошибка ввода",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProcessButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            ResultsPanel.Visibility = Visibility.Collapsed;

            try
            {
                var result = await System.Threading.Tasks.Task.Run(() => ProcessFile(seedStr, inputBytes));
                OrigOutput.Text = result.OrigDisplay;
                KeyOutput.Text = result.KeyDisplay;
                ResultOutput.Text = result.ResultDisplay;
                ResultsPanel.Visibility = Visibility.Visible;

                SaveFile(result.OutputBytes, selectedFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ProcessButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private ProcessResult ProcessFile(string seedStr, byte[] inputBytes)
        {
            var lfsr = new LFSR(seedStr);
            byte[] outputBytes = new byte[inputBytes.Length];

            var allOrigBytes = new System.Collections.Generic.List<string>();
            var allKeyBytes = new System.Collections.Generic.List<string>();
            var allResBytes = new System.Collections.Generic.List<string>();

            for (int i = 0; i < inputBytes.Length; i++)
            {
                byte keyByte = lfsr.NextByte();
                byte resByte = (byte)(inputBytes[i] ^ keyByte);
                outputBytes[i] = resByte;

                allOrigBytes.Add(Convert.ToString(inputBytes[i], 2).PadLeft(8, '0'));
                allKeyBytes.Add(Convert.ToString(keyByte, 2).PadLeft(8, '0'));
                allResBytes.Add(Convert.ToString(resByte, 2).PadLeft(8, '0'));
            }

            string origDisplay, keyDisplay, resDisplay;
            const int firstLimit = 10;
            const int lastLimit = 10;

            if (inputBytes.Length <= firstLimit + lastLimit)
            {
                origDisplay = string.Join(" ", allOrigBytes);
                keyDisplay = string.Join(" ", allKeyBytes);
                resDisplay = string.Join(" ", allResBytes);
            }
            else
            {
                origDisplay = string.Join(" ", allOrigBytes.Take(firstLimit)) +
                              " ... " +
                              string.Join(" ", allOrigBytes.Skip(allOrigBytes.Count - lastLimit));
                keyDisplay = string.Join(" ", allKeyBytes.Take(firstLimit)) +
                             " ... " +
                             string.Join(" ", allKeyBytes.Skip(allKeyBytes.Count - lastLimit));
                resDisplay = string.Join(" ", allResBytes.Take(firstLimit)) +
                             " ... " +
                             string.Join(" ", allResBytes.Skip(allResBytes.Count - lastLimit));
            }

            return new ProcessResult
            {
                OutputBytes = outputBytes,
                OrigDisplay = origDisplay,
                KeyDisplay = keyDisplay,
                ResultDisplay = resDisplay
            };
        }

        private void SaveFile(byte[] data, string originalPath)
        {
            string directory = Path.GetDirectoryName(originalPath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            string newFileName;
            if (originalPath.Contains("processed_"))
            {
                newFileName = originalPath.Replace("processed_", "restored_");
            }
            else
            {
                newFileName = Path.Combine(directory, $"processed_{fileNameWithoutExt}{extension}");
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = Path.GetFileName(newFileName);
            saveFileDialog.Filter = "Все файлы (*.*)|*.*";
            saveFileDialog.DefaultExt = extension;
            saveFileDialog.InitialDirectory = directory;

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(saveFileDialog.FileName, data);
                    MessageBox.Show($"Файл успешно сохранен:\n{saveFileDialog.FileName}",
                                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
    public class ProcessResult
    {
        public byte[] OutputBytes { get; set; }
        public string OrigDisplay { get; set; }
        public string KeyDisplay { get; set; }
        public string ResultDisplay { get; set; }
    }
}