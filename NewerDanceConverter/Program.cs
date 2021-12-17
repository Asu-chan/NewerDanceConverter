using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewerDanceConverter
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("Newer Dance Converter - v1.0.0");
            Console.WriteLine("Converts back and forth NewerSMBW credit dance binary files into text files.");
            Console.WriteLine("Please report me any bugs on discord: Asu-chan#2929\r\n\r\n");

            if(args.Length == 0 || args.Contains("-h")  || args.Contains("--help")  || args.Contains("/?"))
            {
                string toolName = System.AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine("Usage: " + toolName + " <options> <input file path> <output file path>");
                Console.WriteLine("Options: -d or --decode            -> Converts a credit dance binary file into a text file");
                Console.WriteLine("         -e or --encode            -> Converts a text file into a credit dance binary file");
                return;
            }

            int type = (args[0].Equals("-e", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--encode", StringComparison.OrdinalIgnoreCase)) ? 1 : 
                ((args[0].Equals("-d", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--decode", StringComparison.OrdinalIgnoreCase)) ? 2 : 0);

            if (type == 0)
            {
                Console.WriteLine("Invalid argument: " + args[0]);
                return;
            }

            string inPath = args[1];
            string outPath = args[2];
            if (!File.Exists(inPath))
            {
                Console.WriteLine("Couldn't open input file \"" + inPath + "\": File doesn't exist");
                return;
            }
            if (File.Exists(outPath))
            {
                Console.Write("File \"" + outPath + "\" already exists. Do you want to continue and overwrite it? (Y/n): ");
                string answer = Console.ReadLine();
                if(answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Continuing...");
                }
                else
                {
                    Console.WriteLine("Stopping...");
                    return;
                }
            }

            NewerDanceConverter ndc = new NewerDanceConverter();
            int errors = 0;
            switch (type)
            {
                case 1: // Encode
                    errors = ndc.encode(inPath, outPath);
                    if (errors < 0) Console.WriteLine("\r\nEncoding failed.");
                    else if (errors == 0) Console.WriteLine("\r\nEncoding success, no error occured.");
                    else Console.WriteLine("\r\nEncoding success, " + errors + " occured.");
                    break;
                case 2: // Decode
                    errors = ndc.decode(inPath, outPath);
                    if (errors < 0) Console.WriteLine("\r\nDecoding failed.");
                    else if (errors == 0) Console.WriteLine("\r\nDecoding success, no error occured.");
                    else Console.WriteLine("\r\nDecoding success, " + errors + " occured.");
                    break;
            }

        }
    }

    class NewerDanceConverter
    {
        
        public int encode(string inPath, string outPath)
        {
            string[] file = new string[0];
            try
            {
                file = File.ReadAllLines(inPath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't open input file: " + e.Message);
                return -1;
            }

            List<byte> outFile = new List<byte>();

            int errors = 0;

            uint last = 0; ;
            for (int count = 0; count < file.Length; count++)
            {
                try
                {
                    string currentLine = file[count].Replace(" ", "").Replace("\t", "");
                    if (currentLine == "") continue;

                    string[] contents = currentLine.Split(';');
                    string[] timeParts = contents[0].Split(':');

                    byte comm = getCommand(contents[1]);
                    if (comm == 0xFF)
                    {
                        Console.WriteLine("Error: Invalid command on line " + (count + 1) + ": \"" + contents[1] + "\"");
                        errors++;
                        comm = 0;
                    }

                    uint time = (uint)Math.Round((Convert.ToInt32(timeParts[0]) * 3600) + (Convert.ToInt32(timeParts[1]) * 60) + (Convert.ToInt32(timeParts[2]) * 0.6) - 70);
                    if (time > last) last = time;
                    else
                    {
                        Console.WriteLine("Error: Sequence goes backwards at line " + (count + 1));
                        errors++;
                    }

                    outFile.Add((byte)((time & 0xFF000000) >> 24));
                    outFile.Add((byte)((time & 0x00FF0000) >> 16));
                    outFile.Add((byte)((time & 0x0000FF00) >> 8));
                    outFile.Add((byte)((time & 0x000000FF)));
                    outFile.Add(120);
                    outFile.Add(0);
                    outFile.Add(comm);
                    outFile.Add(0);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error: Line " + (count + 1) + " isn't valid.");
                    errors++;
                }
            }
            outFile.Add(0);
            outFile.Add(0);
            outFile.Add(0);
            outFile.Add(0);
            outFile.Add(0);
            outFile.Add(0);
            outFile.Add(0);
            outFile.Add(0);

            try
            {
                File.WriteAllBytes(outPath, outFile.ToArray());
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't write to output file: " + e.Message);
                return -1;
            }

            return errors;
        }
        
        public int decode(string inPath, string outPath)
        {
            byte[] file = new byte[0];
            try
            {
                file = File.ReadAllBytes(inPath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't open input file: " + e.Message);
                return -1;
            }

            if(file.Length % 8 != 0)
            {
                Console.WriteLine("The input file isn't a valid credit dance binary file.");
                return -1;
            }

            List<string> outFile = new List<string>();

            int errors = 0;

            for (int count = 0; count < (file.Length / 8) - 1; count++)
            {
                uint time = BitConverter.ToUInt32(file.Skip(count * 8).Take(4).Reverse().ToArray(), 0) + 70;
                string comm = getCommand(file[count * 8 + 6]);

                if(comm == "INVALID")
                {
                    Console.WriteLine("Error: Invalid command on byte " + (count * 8 + 6) + ". Check line " + (outFile.Count + 1) + " of the output file.");
                    errors++;
                }

                int mins = (int)Math.Floor(time / 3600.0);
                int secs = (int)Math.Floor((time - (mins * 3600.0)) / 60.0);
                int cents = (int)Math.Round((time - (mins * 3600.0) - (secs * 60.0)) / 0.6);

                outFile.Add(mins.ToString("D2") + ":" + secs.ToString("D2") + ":" + cents.ToString("D2") + ";" + comm);
            }

            try
            {
                File.WriteAllLines(outPath, outFile.ToArray());
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't write to output file: " + e.Message);
                return -1;
            }

            return errors;
        }

        public string getCommand(byte commandByte)
        {
            switch(commandByte)
            {
                case 2:
                    return "RIGHT";
                case 4:
                    return "LEFT";
                case 8:
                    return "DUCK";
                case 0x10:
                    return "JUMP";
                case 0x20:
                    return "JUMP2";
                case 0x40:
                    return "SPIN";
                case 0x80:
                    return "CENTRE";
                default:
                    return "INVALID";
            }
        }
        public byte getCommand(string commandString)
        {
            switch(commandString.ToUpper())
            {
                case "RIGHT":
                    return 2;
                case "LEFT":
                    return 4;
                case "DUCK":
                    return 8;
                case "JUMP":
                    return 0x10;
                case "JUMP2":
                    return 0x20;
                case "SPIN":
                    return 0x40;
                case "CENTRE":
                    return 0x80;
                default:
                    return 0xFF;
            }
        }
    }
}
