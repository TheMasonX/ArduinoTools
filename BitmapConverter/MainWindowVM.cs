using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Drawing;
using BitmapConverter.Properties;

namespace BitmapConverter
{
    public class MainWindowVM : ObservableObject
    {
        private const string defaultOutputFileName = "Images";
        private const string? outputFooter = "\n#endif";

        public MainWindowVM ()
        {
            OutputFilePath = Settings.Default.OutputFilePath;
            OpenFiles(new string[] { @"C:\Users\TheMasonX\Pictures\Nona\LED_MATRIX\N.png" });
        }

        #region Properties

        private string? _outputFileName;
        public string OutputFileName
        {
            get => _outputFileName ??= defaultOutputFileName;
            set => SetProperty(ref _outputFileName, value);
        }

        private string? _outputFilePath;
        public string? OutputFilePath
        {
            get => _outputFilePath;
            set
            {
                if(SetProperty(ref _outputFilePath, value))
                {
                    UpdateFileNameFromPath();
                }
            }
        }

        private ObservableCollection<BitmapImage>? _images;
        public ObservableCollection<BitmapImage> Images
        {
            get => _images ??= Application.Current.Dispatcher.Invoke(() => new ObservableCollection<BitmapImage>());
            set => SetProperty(ref _images, value);
        }


        private StringBuilder? _outputTextBuilder;
        public StringBuilder OutputTextBuilder
        {
            get => _outputTextBuilder ??= new StringBuilder();
            set
            {
                if (SetProperty(ref _outputTextBuilder, value))
                    OnPropertyChanged(nameof(OutputText));
            }
        }

        public string OutputText => GetOutputHeader() + OutputTextBuilder.ToString() + outputFooter;


        private Type? _bufferType;
        public Type BufferType
        {
            get => _bufferType ??= typeof(byte);
            set => SetProperty(ref _bufferType, value);
        }

        #endregion Properties

        #region Commands

        private ICommand? _cmdClearFiles;
        public ICommand CMDClearFiles => _cmdClearFiles ??= new RelayCommand(ClearImages);

        private void ClearImages ()
        {
            Task.Run(() =>
            {
                MessageBoxResult result = MessageBox.Show("Clear all images?", "Clear All Images", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
                if (result != MessageBoxResult.OK) return; //User declined

                Application.Current.Dispatcher.Invoke(() => Images.Clear());
            });
        }

        private ICommand? _cmdSave;
        public ICommand CMDSave => _cmdSave ??= new RelayCommand (Save);

        private void Save ()
        {
            if (string.IsNullOrEmpty(OutputFilePath)) //No path, try save as instead
            {
                SaveAs();
                return;
            }

            Task.Run(() =>
            {
                if (File.Exists(OutputFilePath))
                    File.Delete(OutputFilePath);

                using (StreamWriter sw = File.CreateText(OutputFilePath))
                {
                    sw.Write(OutputText);
                    sw.Flush();
                    sw.Close();
                }
            });
        }

        private ICommand? _cmdSaveAs;
        public ICommand CMDSaveAs => _cmdSaveAs ??= new RelayCommand(SaveAs);

        private void SaveAs ()
        {
            Task.Run(() =>
            {
                SaveFileDialog fileDialog = new() { Title = "Save Output .h File", FileName = OutputFileName, OverwritePrompt = true, AddExtension = true, DefaultExt=".h", 
                                                    RestoreDirectory = true, Filter = "Header file (*.h)|*.h|All files (*.*)|*.*" };
                bool? result = fileDialog.ShowDialog();
                if (!result.HasValue || !result.Value) return; //Invalid selection

                OutputFilePath = fileDialog.FileName;

                if (!string.IsNullOrEmpty(OutputFilePath)) Save(); //Valid FilePath
            });
        }

        private ICommand? _cmdOpenFiles;
        public ICommand CMDOpenFiles => _cmdOpenFiles ??= new RelayCommand(BrowseForFiles);

        private void BrowseForFiles ()
        {
            Task.Run(() =>
            {
                OpenFileDialog fileDialog = new() { Title = "Select Images To Convert", RestoreDirectory = true, CheckFileExists = true, Multiselect = true,
                                                    Filter = "Image files (*.bmp, *.jpg, *.png)|*.bmp;*.jpg;*.png|All files (*.*)|*.*" };
                bool? result = fileDialog.ShowDialog();
                if (!result.HasValue || !result.Value) return; //Invalid selection

                OpenFiles(fileDialog.FileNames);
            });
        }


        #endregion Commands

        #region FileMethods

        private void OpenFiles (IEnumerable<string>? files)
        {
            if (!(files?.Any() ?? false)) return; // Invalid Files List

            foreach (var file in files)
            {
                AddImage(file);
            }
            UpdateOutputText();
        }

        private void AddImage(string file)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                BitmapImage image = new(new Uri(file));
                Images.Add(image);
            });
        }

        #endregion FileMethods

        #region OutputText

        private void UpdateOutputText()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OutputTextBuilder.Clear();
                foreach (var image in Images)
                {
                    AddImageToOutput(image);
                }
            });
            OnPropertyChanged(nameof(OutputText));
        }

        private string GetOutputHeader()
        {
            string? defineName = OutputFileName?.ToUpper();
            return $"#ifndef {defineName}\n#define {defineName}\n\n";
        }

        private void AddImageToOutput(BitmapImage image)
        {
            if(image is null) return; // No Image

            if (Images.Where(i => i.UriSource.Equals(image)).Any()) return; //Image already in the list

            string? uri = image?.UriSource?.ToString();
            if (string.IsNullOrEmpty(uri)) return; //Invalid URI

            FileInfo file = new (uri);
            string? fileName = file?.Name?.StripExtension();
            if (string.IsNullOrEmpty(fileName)) return; //Invalid FileName

            //Start the Image Buffer
            OutputTextBuilder.AppendLine($"  \\\\Image buffer auto-generated from {uri}");
            OutputTextBuilder.AppendLine($"  {BufferType.Name} {fileName}[] " + "{");

            //Output Data
            OutputTextBuilder.AppendLine($"    {WriteOutputData(image)}");

            //End the Image Buffer
            OutputTextBuilder.AppendLine("  };");
        }

        private string WriteOutputData(BitmapImage image)
        {
            IEnumerable<string>? text = null;
            IEnumerable<int> data = null;
            data = ConvertImageData(image);

            if (BufferType == typeof(int))
            {
                text = data.Select(d => d.ToString());
            }
            else if (BufferType == typeof(byte))
            {
                text = data.Select(d => $"{(byte)d}");
            }
            else if (BufferType == typeof(bool))
            {
                text = data.Select(d => $"{((d > 0) ? 1 : 0)}");
            }

            return string.Join(", ", text);
        }

        #endregion OutputText

        private void UpdateFileNameFromPath()
        {
            if (string.IsNullOrEmpty(OutputFilePath))
            {
                OutputFileName = defaultOutputFileName;
                return;
            }

            string? name = new FileInfo(OutputFilePath)?.Name?.StripExtension();
            OutputFileName = string.IsNullOrEmpty(name) ? defaultOutputFileName : name;
        }
        

        private List<int> ConvertImageData(BitmapImage image)
        {
            List<int> data = new();
            if (image is null) return data;

            using (Bitmap bitmap = image.BitmapImage2Bitmap())
            {
                if (bitmap is null) return data;

                for (int x = 0; x < bitmap.Width; x++)
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        int pixelARGB = pixel.ToRGB();
                        data.Add(pixelARGB);
                    }
                }
            }

            return data;
        }
    }
}