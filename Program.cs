using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Steganography
{
    class Program
    {
        static int SetBit(int value, int pos) => value |= (1 << pos);
        static int ClearBit(int value, int pos) => value &= ~(1 << pos);
        static bool CheckBit(int value, int pos) => (value & (1 << pos)) != 0;

        static byte SetBit(byte value, int pos) => (byte)SetBit((int)value, pos);
        static byte ClearBit(byte value, int pos) => (byte)ClearBit((int)value, pos);
        static bool CheckBit(byte value, int pos) => CheckBit((int)value, pos);

        static bool[] ByteToBoolArr(byte value)
        {
            bool[] ret = new bool[8];
            for (int i = 0; i < 8; i++)
            {
                ret[i] = CheckBit(value, i);
            }
            return ret;
        }

        static byte BoolArrToByte(bool[] arr)
        {
            byte ret = new byte();
            for (int i = 0; i < 8; i++)
            {
                ret = arr[i] ? SetBit(ret, i) : ClearBit(ret, i);
            }
            return ret;
        }


        static void Main(string[] args)
        {
            if (args.Length >= 2)
            {
                EncodeDataInImage(args[0], args[1]);
            }
            else if (args.Length == 1)
            {
                DecodeDataFromImage(args[0]);
            }
            else
            {
                //Run Test
                if (File.Exists("payload.txt") && File.Exists("mountains.jpg"))
                {
                    File.Copy("payload.txt", "test-payload.txt", true);
                    EncodeDataInImage("test-payload.txt", "mountains.jpg");
                    File.Delete("test-payload.txt");
                    DecodeDataFromImage("mountains.steg.jpg");
                }
            }
        }

        private static void DecodeDataFromImage(string filePath)
        {
            Bitmap bmp = Image.FromFile(filePath) as Bitmap;
            List<byte> buf = new List<byte>();
            bool[] currentArr = new bool[8];
            int pos = 0;
            int fileNameLength = -1;
            int payloadLength = -1;
            bool exit = false;
            for (int x = 0; x < bmp.Width && !exit; x++)
            {
                for (int y = 0; y < bmp.Height && !exit; y++)
                {
                    Color color = bmp.GetPixel(x, y);
                    currentArr[pos++] = CheckBit(color.A, 3);
                    if (pos == 8)
                    {
                        buf.Add(BoolArrToByte(currentArr));
                        pos = 0;
                    }
                    if (buf.Count == 12 && fileNameLength == -1)
                    {
                        //Check for magic
                        byte[] bufArr = buf.ToArray();
                        try
                        {
                            string magic = Encoding.UTF8.GetString(bufArr, 0, 4);
                            if (magic != "STEG")
                            {
                                throw new Exception();
                            }
                            //Console.WriteLine(magic);
                            fileNameLength = BitConverter.ToInt32(bufArr, 4);
                            payloadLength = BitConverter.ToInt32(bufArr, 8);
                        }
                        catch
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Magic not found"); return;
                        }
                    }
                    else if (fileNameLength != -1 && buf.Count == (fileNameLength + payloadLength + 12))
                    {
                        exit = true;
                    }
                }
            }

            Console.WriteLine();

            byte[] data = buf.ToArray();
            string fileName = Encoding.UTF8.GetString(data, 12, fileNameLength);
            Console.WriteLine($"File name was {fileName}");
            byte[] payload = new byte[payloadLength];
            Array.Copy(data, 12 + fileNameLength, payload, 0, payload.Length);
            File.WriteAllBytes(fileName, payload);
        }

        private static void EncodeDataInImage(string payloadPath, string imagePath)
        {
            byte[] data = File.ReadAllBytes(payloadPath);
            byte[] fileName = Encoding.UTF8.GetBytes(payloadPath.Split('\\').Last());
            byte[] header = new byte[12];
            Array.Copy(Encoding.UTF8.GetBytes("STEG"), 0, header, 0, 4); //MAGIC check
            Array.Copy(BitConverter.GetBytes(fileName.Length), 0, header, 4, 4); //FileName Length
            Array.Copy(BitConverter.GetBytes(data.Length), 0, header, 8, 4); //Data length
            List<byte[]> payloadLst = new List<byte[]>();
            payloadLst.Add(header);
            payloadLst.Add(fileName);
            payloadLst.Add(data);
            
            byte[] payload = new byte[payloadLst.Sum(x => x.Length)];
            int offset = 0;
            foreach (byte[] arr in payloadLst)
            {
                Array.Copy(arr, 0, payload, offset, arr.Length);
                offset += arr.Length;
            }
            Console.WriteLine($"Storing {payload.Length * 8} bits");
            int bitsStored = 0;
            try
            {
                string newFile = imagePath.Substring(0, imagePath.LastIndexOf(".")) + ".steg" +
                                 imagePath.Substring(imagePath.LastIndexOf("."));
                
                Bitmap bmp = Image.FromFile(imagePath) as Bitmap;
                Console.WriteLine($"{(bmp.Width * bmp.Height) / 8} Bits Available");
                bmp.MakeTransparent();
                int i = 0;
                int pos = 0;
                for (int x = 0; x < bmp.Width && i < payload.Length; x++)
                {
                    for (int y = 0; y < bmp.Height && i < payload.Length; y++)
                    {
                        bool currentBit = CheckBit(payload[i], pos++);
                        ++bitsStored;
                        if (pos == 8)
                        {
                            pos = 0;
                            i++;
                        }
                        Color color = bmp.GetPixel(x, y);
                        int newAlpha = currentBit ? SetBit(color.A, 3) : ClearBit(color.A, 3);
                        bmp.SetPixel(x, y, Color.FromArgb(newAlpha, color.R, color.G, color.B));
                    }
                }

                bmp.Save(newFile, ImageFormat.Png);
                Console.WriteLine($"{bitsStored} bits stored!");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to load image {imagePath}");
            }
        }
    }
}
