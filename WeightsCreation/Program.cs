using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace WeightsCreation
{
    public class Program
    {
        public char[] delimiter;// = new char[] { ',' /*'\t'*/ };
        public string delimiterString = ",";
        string StartupPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        static void Main(string[] args)
        {
        }
        public static void WeightCreate/*SingleThread*/(string targetName, string idName, string weightName, char delimiter, string StartupPath)
        {
            string BasePath = StartupPath + @"\..\..\..\..\..\1_INPUT_SPLIT_WEIGHTCREATION\";
            string BufferPath = StartupPath + @"\..\..\..\..\..\3_RANGER_BUFFER_DATA\";
            string[] fileEntries = Directory.GetFiles(BasePath, "*"/*, SearchOption.AllDirectories*/);

            foreach (var directory in fileEntries)
            {
                string Name = directory.Replace(BasePath, "").Replace(/*".txt"*/ ".csv", "");

                Console.WriteLine("{0} Memory: {1}", Name, GC.GetTotalMemory(false));
                Console.WriteLine("{0} Reading", Name);

                int NumRows = TotalLines(directory);

                Console.WriteLine("{0} Memory: {1}", Name, GC.GetTotalMemory(false));

                double[] WeightsNoak = new double[NumRows - 1]; //Header

                using (FileStream FileStreamSource = File.OpenRead(directory))
                {
                    using (var SourceStreamReader = new StreamReader(FileStreamSource))
                    {
                        string FirstLineStringKomma = SourceStreamReader.ReadLine();
                        string[] FirstLine = FirstLineStringKomma.Split(delimiter);
                        string FirstLineStringTab = String.Join("\t", FirstLine);
                        Console.WriteLine("{0} Memory: {1}", Name, GC.GetTotalMemory(false));

                        int NumColumns = FirstLine.Length;
                        int positionWeight = -1;

                        SourceStreamReader.DiscardBufferedData();
                        SourceStreamReader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
                        for (int i = 0; i < NumRows; i++)
                        {
                            if (i % 150 == 0)
                            {
                                Console.WriteLine("{0} Memory: {1}", Name, GC.GetTotalMemory(false));
                            }
                            string s3 = SourceStreamReader.ReadLine();
                            string[] CurrentLine = s3.Split(delimiter);

                            if(i == 0)
                            {
                                positionWeight = Array.FindIndex(CurrentLine, str => str==weightName);
                            }
                            else
                            {
                                if (CurrentLine[positionWeight] == "1")
                                {
                                    WeightsNoak[i - 1] = 1.0;
                                }
                                else if (CurrentLine[positionWeight] == "0")
                                {
                                    WeightsNoak[i - 1] = 0.0;
                                }
                            }
                            string CurrentLine2 = String.Join("\t", CurrentLine);
                        }

#region CorrCalc
                        int NumEvents = WeightsNoak.Where(i => i == 1.0).Count();
                        int NumNonEvents = WeightsNoak.Length - NumEvents;

                        double CorrectionWeight_Non_Event = 1;
                        double CorrectionWeight_Event = NumNonEvents/ (double)NumEvents;

                        double[] WeightsNoak2 = WeightsNoak.Select(i => i == 0.0 ? CorrectionWeight_Non_Event : CorrectionWeight_Event).ToArray();
                        string[] WeightsStringArray = new string[] { String.Join(" ", WeightsNoak2) };

                        WeightsStringArray = WeightsStringArray.Select(i => i.Replace(",", ".")).ToArray();
                        System.IO.File.WriteAllLines(BufferPath + Name.Replace(".txt", "") + "_W" + ".txt", WeightsStringArray);
#endregion
                    }
                }
            }
        }
        public static int TotalLines(string filePath)
        {
            using (StreamReader r = new StreamReader(filePath))
            {
                int i = 0;
                while (r.ReadLine() != null) { i++; }
                return i;
            }
        }
    }
}
