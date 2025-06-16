using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Input;
using System.Linq;
using OpenAI.Chat;

namespace GitExtractor
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitExtractor", "settings.txt");

        private string? folderPath;
        public string? FolderPath
        {
            get => folderPath;
            set
            {
                if (folderPath != value)
                {
                    folderPath = value;
                    OnPropertyChanged(nameof(FolderPath));
                }
            }
        }

        private DateTime? selectedWeek = DateTime.Today;
        public DateTime? SelectedWeek
        {
            get => selectedWeek;
            set
            {
                if (selectedWeek != value)
                {
                    selectedWeek = value;
                    OnPropertyChanged(nameof(SelectedWeek));
                }
            }
        }

        public ICommand SelectFolderCommand { get; }
        public ICommand ExtractCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel()
        {
            SelectFolderCommand = new RelayCommand(_ => SelectFolder());
            ExtractCommand = new RelayCommand(_ => Extract());
            LoadSettings();
        }

        private void SelectFolder()
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                FolderPath = dialog.SelectedPath;
                SaveFolderPath(dialog.SelectedPath);
            }
        }

        private void Extract()
        {
            if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
            {
                System.Windows.MessageBox.Show("Please select a valid folder first.");
                return;
            }

            var selectedDate = SelectedWeek ?? DateTime.Today;
            var startOfWeek = selectedDate.Date.AddDays(-(int)selectedDate.DayOfWeek + (int)DayOfWeek.Monday);
            var endOfWeek = startOfWeek.AddDays(7);

            var files = new List<string>();
            foreach (var file in Directory.GetFiles(FolderPath))
            {
                if (TryGetDateFromFileName(Path.GetFileName(file), out var fileDate))
                {
                    if (fileDate >= startOfWeek && fileDate < endOfWeek)
                    {
                        files.Add(File.ReadAllText(file));
                    }
                }
            }

            if (files.Count == 0)
            {
                System.Windows.MessageBox.Show("No files found for the selected week.");
                return;
            }

            var diffText = string.Join("\n\n", files);

            try
            {
                const string ApiKey = "<YOUR_OPENAI_API_KEY>"; // TODO: replace with your API key
                ChatClient client = new("gpt-4o", ApiKey);
                string prompt = "Analyze the following git diff files and provide your insights:\n\n" + diffText;
                ChatCompletion completion = client.CompleteChat(prompt);
                string response = completion.Content.FirstOrDefault()?.Text ?? "No response";
                System.Windows.MessageBox.Show(response, "OpenAI Analysis");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to call OpenAI API: {ex.Message}");
            }
        }

        private bool TryGetDateFromFileName(string fileName, out DateTime date)
        {
            date = default;
            var match = Regex.Match(fileName, @"\d{4}-\d{2}-\d{2}");
            if (match.Success && DateTime.TryParse(match.Value, out date))
            {
                return true;
            }

            match = Regex.Match(fileName, @"\d{8}");
            if (match.Success && DateTime.TryParseExact(match.Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out date))
            {
                return true;
            }
            return false;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    FolderPath = File.ReadAllText(settingsPath);
                }
            }
            catch
            {
                // ignore errors when loading settings
            }
        }

        private void SaveFolderPath(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(settingsPath, path);
            }
            catch
            {
                // ignore errors when saving settings
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
