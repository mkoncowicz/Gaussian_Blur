using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace GaussianBlur
{
    public struct ColourPixel
    {
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte Alpha;

        public ColourPixel(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }
    }
    public unsafe class BlurProcessor
    {
        [DllImport("GaussianBlurAsm.dll")]
        private static extern void AsmGaussianBlur(int arraySize, int imageWidth, ushort* red, ushort* green, ushort* blue, ushort* outRed, ushort* outGreen, ushort* outBlue);
        public void ApplyAsmBlur(int arraySize, int imageWidth, ushort* red, ushort* green, ushort* blue, ushort* outRed, ushort* outGreen, ushort* outBlue)
        {
            AsmGaussianBlur(arraySize, imageWidth, red, green, blue, outRed, outGreen, outBlue);
        }

        [DllImport("GaussianBlurCpp.dll")]
        private static extern void CppGaussianBlur(int arraySize, int imageWidth, ushort* red, ushort* green, ushort* blue, ushort* outRed, ushort* outGreen, ushort* outBlue);
        public void ApplyCppBlur(int arraySize, int imageWidth, ushort* red, ushort* green, ushort* blue, ushort* outRed, ushort* outGreen, ushort* outBlue)
        {
            CppGaussianBlur(arraySize, imageWidth, red, green, blue, outRed, outGreen, outBlue);
        }
    }

    public partial class MainWindow : Window
    {
        private int _repetitions = 1;
        private Stopwatch _timer = new Stopwatch();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Uri imageUri = new Uri(openFileDialog.FileName);
                PictureBox1.Source = new BitmapImage(imageUri);
            }
        }

        private void ExecuteBlur_Click(object sender, RoutedEventArgs e)
        {
            if (!(PictureBox1.Source is BitmapImage sourceBitmap))
            {
                System.Windows.MessageBox.Show("No image loaded.");
                return;
            }

            Bitmap originalBitmap = ConvertToBitmap(sourceBitmap);
            Bitmap cppBitmap = new Bitmap(originalBitmap);
            Bitmap asmBitmap = new Bitmap(originalBitmap);

            PerformBlur(cppBitmap, asmBitmap);

            PictureBox2.Source = ConvertToBitmapImage(cppBitmap);
            PictureBox3.Source = ConvertToBitmapImage(asmBitmap);
        }

        private void PerformBlur(Bitmap cppBitmap, Bitmap asmBitmap)
        {
            int width = cppBitmap.Width;
            int height = cppBitmap.Height;
            int newWidth = width + 2;
            int newHeight = height + 2;

            Bitmap extendedCppBitmap = ExtendBitmap(cppBitmap, newWidth, newHeight);
            Bitmap extendedAsmBitmap = ExtendBitmap(asmBitmap, newWidth, newHeight);

            extendedCppBitmap.Save("extendedBitmapCpp.bmp", ImageFormat.Bmp);
            extendedAsmBitmap.Save("extendedBitmapAsm.bmp", ImageFormat.Bmp);

            ColourPixel[] cppPixelData = ConvertBitmapToPixelArray(extendedCppBitmap);
            ColourPixel[] asmPixelData = ConvertBitmapToPixelArray(extendedAsmBitmap);

            ushort[] inRed, inGreen, inBlue, outCppRed, outCppGreen, outCppBlue, outAsmRed, outAsmGreen, outAsmBlue;
            PrepareArrays(cppPixelData, newWidth, newHeight, out inRed, out inGreen, out inBlue, out outCppRed, out outCppGreen, out outCppBlue);
            PrepareArrays(asmPixelData, newWidth, newHeight, out inRed, out inGreen, out inBlue, out outAsmRed, out outAsmGreen, out outAsmBlue);

            long cppBlurTime = ApplyCppBlur(width, height, newWidth, inRed, inGreen, inBlue, outCppRed, outCppGreen, outCppBlue, cppBitmap);
            long asmBlurTime = ApplyAsmBlur(width, height, newWidth, inRed, inGreen, inBlue, outAsmRed, outAsmGreen, outAsmBlue, asmBitmap);

            string resultText = $"\n\nGaussian Blur (C++): {cppBlurTime} ms\n\nGaussian Blur (Assembler): {asmBlurTime} ms\n";
            Dispatcher.Invoke(() =>
            {
                textoutput.Text += resultText;
            });

            string filePath = "GaussianBlurResults.txt";

            File.WriteAllText(filePath, resultText);

            cppBitmap.Save("processedBitmapCpp.jpg", ImageFormat.Jpeg);
            asmBitmap.Save("processedBitmapAsm.jpg", ImageFormat.Jpeg);
        }

        private Bitmap ExtendBitmap(Bitmap original, int newWidth, int newHeight)
        {
            Bitmap extended = new Bitmap(newWidth, newHeight);
            using (Graphics gfx = Graphics.FromImage(extended))
            {
                gfx.FillRectangle(Brushes.White, 0, 0, newWidth, newHeight);
                gfx.DrawImage(original, 1, 1, original.Width, original.Height);
            }
            return extended;
        }

        private ColourPixel[] ConvertBitmapToPixelArray(Bitmap bitmap)
        { 
            int width = bitmap.Width;
            int height = bitmap.Height;
            ColourPixel[] pixelData = new ColourPixel[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixelColor = bitmap.GetPixel(x, y);
                    pixelData[y * width + x] = new ColourPixel(pixelColor.R, pixelColor.G, pixelColor.B, pixelColor.A);
                }
            }
            return pixelData;
        }

        private void PrepareArrays(ColourPixel[] pixelData, int newWidth, int newHeight, out ushort[] inRed, out ushort[] inGreen, out ushort[] inBlue, out ushort[] outRed, out ushort[] outGreen, out ushort[] outBlue)
        {
            int arraySize = newWidth * newHeight;
            inRed = new ushort[arraySize];
            inGreen = new ushort[arraySize];
            inBlue = new ushort[arraySize];
            outRed = new ushort[arraySize];
            outGreen = new ushort[arraySize];
            outBlue = new ushort[arraySize];

            for (int i = 0; i < arraySize; i++)
            {
                inRed[i] = pixelData[i].Red;
                inGreen[i] = pixelData[i].Green;
                inBlue[i] = pixelData[i].Blue;
                outRed[i] = (byte)255;
                outGreen[i] = (byte)255;
                outBlue[i] = (byte)255;
            }
        }

        private long ApplyCppBlur(int width, int height, int newWidth, ushort[] inRed, ushort[] inGreen, ushort[] inBlue, ushort[] outRed, ushort[] outGreen, ushort[] outBlue, Bitmap bitmap)
        {
            BlurProcessor processor = new BlurProcessor();
            int arraySize = width * height;
            Stopwatch stopwatch = new Stopwatch();

            unsafe
            {
                fixed (ushort* in_Red = inRed, in_Green = inGreen, in_Blue = inBlue, out_Red = outRed, out_Green = outGreen, out_Blue = outBlue)
                {
                    stopwatch.Start();
                    processor.ApplyCppBlur(arraySize, newWidth, in_Red, in_Green, in_Blue, out_Red, out_Green, out_Blue);
                    stopwatch.Stop();
                }
            }
       
            SetProcessedPixels(bitmap, outRed, outGreen, outBlue);
            return stopwatch.ElapsedMilliseconds;
        }

        private long ApplyAsmBlur(int width, int height, int newWidth, ushort[] inRed, ushort[] inGreen, ushort[] inBlue, ushort[] outRed, ushort[] outGreen, ushort[] outBlue, Bitmap bitmap)
        {
            BlurProcessor processor = new BlurProcessor();
            int arraySize = width * height;
            Stopwatch stopwatch = new Stopwatch();
     
            unsafe
            {
                fixed (ushort* in_Red = inRed, in_Green = inGreen, in_Blue = inBlue, out_Red = outRed, out_Green = outGreen, out_Blue = outBlue)
                {
                    stopwatch.Start();
                    processor.ApplyAsmBlur(width * height, newWidth, in_Red, in_Green, in_Blue, out_Red, out_Green, out_Blue);
                    stopwatch.Stop();                   
                }
            }
                      
            SetProcessedPixels(bitmap, outRed, outGreen, outBlue);
            return stopwatch.ElapsedMilliseconds;
        }

        private void SetProcessedPixels(Bitmap bitmap, ushort[] outRed, ushort[] outGreen, ushort[] outBlue)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = Color.FromArgb(outRed[y * width + x], outGreen[y * width + x], outBlue[y * width + x]);
                    bitmap.SetPixel(x, y, color);
                }
            }
        }

        private Bitmap ConvertToBitmap(BitmapImage bitmapImage)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(memoryStream);
                return new Bitmap(memoryStream);
            }
        }

        private BitmapImage ConvertToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, ImageFormat.Png);
                memoryStream.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }

        private void RepetitionsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _repetitions = (int)slider.Value;
            if (repetitions_textbox!=null)
            {
                repetitions_textbox.Text = $"NUMBER OF REPETITIONS: {_repetitions}";
            }
        }
    }
}
