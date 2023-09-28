using BitmapConverter.Properties;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BitmapConverter
{
    public class MainWindowVM : ObservableObject
    {
        private const string defaultOutputFileName = "Images";
        private const string? outputFooter = "#endif";

        public MainWindowVM ()
        {
            OutputFilePath = Settings.Default.OutputFilePath;
            OutputFileName = Settings.Default.OutputFileName;
            Inverted = Settings.Default.Inverted;
            //OpenFiles(new string[] { @"C:\Users\TheMasonX\Pictures\Nona\LED_MATRIX\N.png" });
        }

        #region Properties

        private string? _outputFileName;
        public string OutputFileName
        {
            get => _outputFileName ??= defaultOutputFileName;
            set
            {
                if(SetProperty(ref _outputFileName, value))
                {
                    Settings.Default.OutputFileName = OutputFileName;
                    Settings.Default.Save();
                    OnPropertyChanged(nameof(OutputText));
                }
            }

        }

        private string? _outputFilePath;
        public string? OutputFilePath
        {
            get => _outputFilePath;
            set
            {
                if(SetProperty(ref _outputFilePath, value))
                {
                    Settings.Default.OutputFilePath = OutputFilePath;
                    Settings.Default.Save();
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

        private BitmapImage _selectedImage;
        public BitmapImage SelectedImage
        {
            get => _selectedImage;
            set => SetProperty(ref _selectedImage, value);
        }

        private bool _inverted;
        public bool Inverted
        {
            get => _inverted;
            set
            {
                if (SetProperty(ref _inverted, value))
                {
                    UpdateOutputText();
                    Settings.Default.Inverted = Inverted;
                    Settings.Default.Save();
                }
            }
        }

        private IOutput _outputType;
        public IOutput OutputType
        {
            get => _outputType ??= OutputTypes[0];
            set
            {
                if (SetProperty(ref _outputType, value))
                    UpdateOutputText();
            }
        }

        private static IOutput[] _outputTypes = { new OutputMonoInt(), new OutputRGBInt(), new OutputBool() };
        public IOutput[] OutputTypes => _outputTypes;

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
                UpdateOutputText();
            });
        }

        private ICommand? _cmdRemoveFile;
        public ICommand CMDRemoveFile => _cmdRemoveFile ??= new RelayCommand<object>(RemoveFile);

        private void RemoveFile (object? file)
        {
            if (!(file is BitmapImage image)) return; //Param is not an image

            Application.Current.Dispatcher.Invoke(() => Images.Remove(image));
            Task.Run(UpdateOutputText);
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
                if (!string.IsNullOrEmpty(Settings.Default.OutputDirectory))
                    fileDialog.InitialDirectory = Settings.Default.OutputDirectory;

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
                Uri uri = new(file);
                if (Images.Where(i => i.UriSource.Equals(uri)).Any()) return; //Image already loaded
                BitmapImage image = new(uri);
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
            OutputTextBuilder.AppendLine($"  //Image buffer auto-generated from {uri}");
            OutputTextBuilder.AppendLine($"  const {OutputType.TypeName} {fileName}[{(int)image.Width}][{(int)image.Height}] PROGMEM " + "{");

            //Output Data
            OutputTextBuilder.AppendLine($"    {ImageToTextBuffer(image)}");

            //End the Image Buffer
            OutputTextBuilder.AppendLine("  };\n");
        }

        private string ImageToTextBuffer(BitmapImage image)
        {
            if (image is null) return ""; //No image

            List<Color> data = image.GetPixels();
            if (!data.Any()) return ""; //No pixel data

            List<string> rowText = new();
            List<string> colText = new();

            for (int y = 0; y < (int)image.Height; y++)
            {
                for (int x = 0; x < (int)image.Width; x++)
                {
                    int index = image.GetIndex(x, y);
                    string pixelData = OutputType.FormatColor(data[index], Inverted);
                    rowText.Add(pixelData);
                }
                colText.Add("{ " + string.Join(",\t", rowText) + " },");
                rowText.Clear();
            }
            return string.Join("\n    ", colText);
        }

        #endregion OutputText

        private void UpdateFileNameFromPath()
        {
            if (string.IsNullOrEmpty(OutputFilePath))
            {
                OutputFileName = defaultOutputFileName;
                return;
            }

            FileInfo fileInfo = new (OutputFilePath);
            string? name = fileInfo?.Name?.StripExtension();
            if (!string.IsNullOrEmpty(name))
            {
                OutputFileName = name;
                Settings.Default.OutputDirectory = fileInfo.DirectoryName;
                Settings.Default.Save();
            }
            else
                OutputFileName = defaultOutputFileName;
        }
    }
}