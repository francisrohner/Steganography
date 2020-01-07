using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
                EncodeDataInImage(args);
            }
            else if (args.Length == 1)
            {
                DecodeDataFromImage(args);
            }
        }

        private static void DecodeDataFromImage(string[] args)
        {
            Bitmap bmp = Image.FromFile(args[0]) as Bitmap;
            List<byte> buf = new List<byte>();
            bool[] currentArr = new bool[8];
            int pos = 0;
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
                    if (buf.Count == 8 && payloadLength == -1)
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
                            Console.WriteLine(magic);
                        }
                        catch
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Magic not found"); return;
                        }

                        payloadLength = BitConverter.ToInt32(bufArr, 4);
                        
                        Console.WriteLine($"{payloadLength} payload length");
                    }
                    else if (payloadLength != -1 && buf.Count == (payloadLength + 8))
                    {
                        exit = true;
                    }
                }
            }

            Console.WriteLine();

            byte[] data = buf.ToArray();
            string msg = Encoding.UTF8.GetString(data, 8, payloadLength);
            Console.WriteLine(msg);
        }

        private static void EncodeDataInImage(string[] args)
        {
            byte[] payload = File.ReadAllBytes(args[0]);
            //return;
            byte[] header = new byte[8];
            Array.Copy(Encoding.UTF8.GetBytes("STEG"), 0, header, 0, 4); //MAGIC check
            Array.Copy(BitConverter.GetBytes(payload.Length), 0, header, 4, 4);

            byte[] newPayload = new byte[payload.Length + 8];
            Array.Copy(header, 0, newPayload, 0, 8);
            Array.Copy(payload, 0, newPayload, 8, payload.Length);
            payload = newPayload;

            Console.WriteLine($"Storing {payload.Length * 8} bits");
            //BitConverter.GetBytes(0).Length
            //int lastX, lastY;
            int bitsStored = 0;
            try
            {
                string newFile = args[1].Substring(0, args[1].LastIndexOf(".")) + ".steg" +
                                 args[1].Substring(args[1].LastIndexOf("."));
                Bitmap bmp = Image.FromFile(args[1]) as Bitmap;
                bmp.MakeTransparent();
                //int spaceCounter = 0;
                //int spacing = 10;
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
                Console.WriteLine($"Failed to load image {args[1]}");
            }
        }
    }
}
