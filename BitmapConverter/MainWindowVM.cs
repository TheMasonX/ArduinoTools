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

        public MainWindowVM()
        {
            OutputFilePath = Settings.Default.OutputFilePath;
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

        public string OutputText => OutputTextBuilder.ToString();


        private Type? _bufferType;
        public Type BufferType
        {
            get => _bufferType ??= typeof(byte);
            set => SetProperty(ref _bufferType, value);
        }

        #endregion Properties

        #region Commands

        private ICommand? _cmdOpenFiles;
        public ICommand CMDOpenFiles => _cmdOpenFiles ??= new RelayCommand(OpenFiles);
        
        private void OpenFiles()
        {
            Task.Run(() =>
            {
                OpenFileDialog fileDialog = new() { Title = "Select Images To Convert", RestoreDirectory = true, CheckFileExists=true, Multiselect = true, Filter = "Image files (*.bmp, *.jpg, *.png)|*.bmp;*.jpg;*.png|All files (*.*)|*.*" };
                bool? result = fileDialog.ShowDialog();
                if (!result.HasValue || !result.Value) return; //Invalid selection

                InitOutput();
                ClearImages();
                foreach (var file in fileDialog.FileNames)
                {
                    AddImage(file);
                }
                EndOutput();
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
                SaveFileDialog fileDialog = new() { Title = "Save Output .h File", FileName = OutputFileName, OverwritePrompt = true, AddExtension = true, DefaultExt=".h", RestoreDirectory = true, Filter = "Header file (*.h)|*.h|All files (*.*)|*.*" };
                bool? result = fileDialog.ShowDialog();
                if (!result.HasValue || !result.Value) return; //Invalid selection

                OutputFilePath = fileDialog.FileName;

                if (!string.IsNullOrEmpty(OutputFilePath)) Save();
            });
        }


        #endregion Commands

        private void ClearImages()
        {
            Application.Current.Dispatcher.Invoke(() => Images.Clear());
        }

        private void AddImage(string file)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                BitmapImage image = new(new Uri(file));
                Images.Add(image);
                AddImageToOutput(image);
            });
        }

        private void InitOutput()
        {
            OutputTextBuilder.Clear();

            string defineName = OutputFileName.ToUpper();
            OutputTextBuilder.AppendLine($"#ifndef {defineName}");
            OutputTextBuilder.AppendLine($"#define {defineName}\n");
        }

        private void AddImageToOutput(BitmapImage image)
        {
            if(image is null) return; // No Image

            string? uri = image?.UriSource?.ToString();
            if (string.IsNullOrEmpty(uri)) return; //Invalid URI

            FileInfo file = new (uri);
            string fileName = StripExtension(file?.Name);
            if (string.IsNullOrEmpty(fileName)) return; //Invalid FileName

            //Start the Image Buffer
            OutputTextBuilder.Append($"  {BufferType.Name} {fileName}[]");
            OutputTextBuilder.AppendLine(" {"); //Doesn't like string interpolation with this character //TODO: Fix

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

        private void EndOutput()
        {
            OutputTextBuilder.AppendLine("\n#endif");
            OnPropertyChanged(nameof(OutputText));
        }

        private void UpdateFileNameFromPath()
        {
            if (string.IsNullOrEmpty(OutputFilePath))
            {
                OutputFileName = defaultOutputFileName;
                return;
            }

            string name = StripExtension(new FileInfo(OutputFilePath)?.Name);
            if (string.IsNullOrEmpty(name))
            {
                OutputFileName = defaultOutputFileName;
                return;
            }

            OutputFileName = name;
        }

        private string StripExtension(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return ""; //Invalid fileName

            Regex regex = new Regex(@"\..*");
            return regex.Replace(fileName, "");
        }

        //private List<T> ConvertImageData<T>(BitmapImage image) where T : IConvertible
        private List<int> ConvertImageData(BitmapImage image)
        {
            List<int> data = new();
            if (image is null) return data;

            using (Bitmap bitmap = BitmapImage2Bitmap(image))
            {
                if (bitmap is null) return data;

                for (int x = 0; x < bitmap.Width; x++)
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        int pixelARGB = ToRGB(pixel);
                        data.Add(pixelARGB);
                    }
                }
            }

            return data;
        }

        private int ToRGB(Color color)
        {
            return ((color.R & 0x0ff) << 16) | ((color.G & 0x0ff) << 8) | (color.B & 0x0ff);
        }

        private Bitmap BitmapImage2Bitmap (BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new(outStream);

                return new Bitmap(bitmap);
            }
        }
    }
}