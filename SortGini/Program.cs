
#define RangerGewichtung
#define RAMOptimized
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace SortGini
{
    public class Program
    {
        public static string StartupPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string INPUT_DATA_PATH = StartupPath + @"\..\..\..\..\..\1_INPUT_SPLIT_WEIGHTCREATION\";
        //string INPUT_DATA_PATH = StartupPath + @"\..\..\..\..\..\2_OUTPUT_SPLIT_WEIGHTCREATION\";
        string BUFFER_DATA_PATH = StartupPath + @"\..\..\..\..\..\3_RANGER_BUFFER_DATA\";
        string IMPORTANCE_MEASURES_PATH = StartupPath + @"\..\..\..\..\..\4_RANGER_IMPORTANCE_MEASURES\";
        //string OUTPUT_DATA_PATH = StartupPath + @"\..\..\..\..\..\5_RANGER_OUTPUT_DATA\";
        public static string targetPath_Learn = StartupPath + @"\..\..\..\..\..\6_LEARNDATA_SAS_COL\";
        public static string targetPath_Test = StartupPath + @"\..\..\..\..\..\6_TESTDATA_SAS_COL\";
        string IMPORTANCE_MEASURE_NAME;
        string TARGET;
        //string NOAK_NAME_TABLE;
        int AnzahlHerausziehen = 100;
        string FILENAME;
        char[] delimiter = new char[] { '\t' };

        static void Main(string[] args)
        {
            string targetName = "B_kz_frueh";
            string idName = "I_ID";
            string weightName = "B_hdg001";

            string delimiter = "\t";
            char delimiterChar = delimiter[0];

            if(weightName != "") //Um das Korrekturgewicht zu berechnen muss leider der gesamte Datensatz einmal durchlaufen werden
            {
                WeightsCreation.Program.WeightCreate(targetName, idName, weightName, delimiterChar, StartupPath);
            }

            //Ranger
            Program p2 = new Program();
            p2.StartRanger(targetName, idName, weightName);
        }
#if RAMOptimized
        public void StartRanger(string targetName, string idName, string weightName)
        {
            string[] fileEntries = Directory.GetFiles(INPUT_DATA_PATH, "*" /*, SearchOption.AllDirectories */);
            fileEntries = fileEntries.Where(x => !(x.Substring(x.Length - 6) == "_W.txt")).ToArray();

            foreach (string DIRECTORY in fileEntries)
            {
                int NumRows = TotalLines(DIRECTORY);
                FILENAME = DIRECTORY.Replace(INPUT_DATA_PATH, "").Replace(".txt", "");
                Console.WriteLine("Reading and Preparing {0}", FILENAME);
                using (FileStream FileStreamSource = File.OpenRead(DIRECTORY))
                using (FileStream FileStreamBufferWrite = new FileStream(BUFFER_DATA_PATH + FILENAME + ".txt", FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
                using (FileStream FileStreamBufferRead = new FileStream(BUFFER_DATA_PATH + FILENAME + ".txt", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (FileStream FileStreamTargetWrite = new FileStream(targetPath_Learn + FILENAME + "_Learn.txt", FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
                {
                    using (StreamReader SourceStreamReader = new StreamReader(FileStreamSource))
                    using (StreamWriter BufferStreamWriter = new StreamWriter(FileStreamBufferWrite))
                    using (StreamReader BufferStreamReader = new StreamReader(FileStreamBufferRead))
                    using (StreamWriter TargetStreamWriter = new StreamWriter(FileStreamTargetWrite))
                    {
                        string FirstLine = SourceStreamReader.ReadLine();
                        SourceStreamReader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);

                        TARGET = FirstLine.Split(delimiter)[0];
                        IMPORTANCE_MEASURE_NAME = "ICD_" + TARGET.Replace("B_T_", "") + "_" + "IMPORTANCE_MEASURE.txt";
                        Steuerung(SourceStreamReader, BufferStreamWriter, BufferStreamReader, TargetStreamWriter, NumRows, targetName, idName, weightName);
                    }
                }
                File.Delete(BUFFER_DATA_PATH + FILENAME + ".txt");
                File.Delete(BUFFER_DATA_PATH + FILENAME + "_W.txt");

                string learn = targetPath_Learn + FILENAME + "_Learn.txt";
                string test = targetPath_Test + FILENAME + "_Test.txt";

                if (File.Exists(test))
                    File.Delete(test);

                File.Copy(learn, test);
            }
        }
        public void Steuerung(StreamReader SourceStreamReader, StreamWriter BufferStreamWriter, StreamReader BufferStreamReader, StreamWriter TargetStreamWriter, int NumRows, string targetName, string idName, string weightName)
        {
            //TO DO:
            //KATEGORIALE VARIABLEN BEIM RANGEN SPEZIFIZIEREN

            List<string> ausschlüsse = new List<string>();
            //ausschlüsse.Add("W_KOMBI_KORREKTUR_FAKTOR");
            //ausschlüsse.Add("C_alter");
            ausschlüsse.Add("I_VERSNR"); //Zur Sicherheit, ausschließen, falls vorhanden ist
            ausschlüsse.Add("I_ID");
            ausschlüsse.Add(idName);
            ausschlüsse.Add(weightName);

            InputVorbereiten(SourceStreamReader, ausschlüsse, BufferStreamWriter, NumRows);
            //Zwischentabellen Rausschreiben
            //System.IO.File.WriteAllLines(BUFFER_DATA_PATH + FILENAME + ".txt", work);

            ExecuteCommand(FILENAME, TARGET, weightName);

            //File.Delete(BUFFER_DATA_PATH + FILENAME + ".txt");
            //Vom Ranger selektierte Variablen
            List<string> SelectedVariables = Sortieren(weightName);

           
            //Wieder Hinzufügen des Targets und der I_Variablen
            SelectedVariables.Add(TARGET);
            //SelectedVariables.Add("W_KOMBI_KORREKTUR_FAKTOR");
            SelectedVariables.Add("I_VERSNR");
            SelectedVariables.Add("I_ID");
            //SelectedVariables.Add("C_alter");

            SelectedVariables.Add(idName);
            SelectedVariables.Add(weightName); //richtig?

            Select_EINSCHLÜSSE(SourceStreamReader, TargetStreamWriter, SelectedVariables, NumRows);

        }

        public void ExecuteCommand(string Inputpath_Buffer, string NOAK_Name, string weightName)
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                RedirectStandardInput = true
            };
            System.Diagnostics.Process proc = new System.Diagnostics.Process() { StartInfo = psi };

            proc.Start();

            proc.StandardInput.Write(CommandScript_Ranger(FILENAME, NOAK_Name, weightName));

            proc.WaitForExit();
            proc.Close();
        }
        private string CommandScript_Ranger(string FILENAME, string NOAK_Name, string weightName)
        {
            StringBuilder sb = new StringBuilder();
            // VERZEICHNIS SETZEN
            sb.Append(@"cd " + StartupPath + @"\..\..\..\..\.." + Environment.NewLine);
            sb.Append(@"ranger64 --verbose --file .\3_RANGER_BUFFER_DATA\" + FILENAME + ".txt");
            sb.Append(@" --depvarname " + NOAK_Name);
            sb.Append(@" --treetype 1 --nthreads 4");
            sb.Append(@" --impmeasure 5"); /* --impmeasure 1*/ /* 1 <- GINI, 2 <- Permutation Importance 5 <- die korrigierte Impurity Importance (5) ist deutlich schneller*/
            if(weightName!="")
                sb.Append(@" --caseweights .\3_RANGER_BUFFER_DATA\" + FILENAME + "_W.txt");
            sb.Append(Environment.NewLine);

            //UMBENNEN
            sb.Append(@"rename ranger_out.importance " + IMPORTANCE_MEASURE_NAME + Environment.NewLine);

            //VERSCHIEBEN
            sb.Append(@"move /Y " + IMPORTANCE_MEASURE_NAME + @" .\" + "4_RANGER_IMPORTANCE_MEASURES" + Environment.NewLine);

            sb.Append("Exit" + Environment.NewLine);
            return sb.ToString();
        }
        private List<string> Sortieren(string weightName)
        {
            string[] lines = System.IO.File.ReadAllLines(IMPORTANCE_MEASURES_PATH + IMPORTANCE_MEASURE_NAME);

            Dictionary<string, double> dict1 = new Dictionary<string, double>();
            char delimiterChar;
            //if (weightName == "") //merkwürdiges Verhalten des Rangers
            //    delimiterChar = ' ';
            //else
            //    delimiterChar = '\t';
            delimiterChar = ' ';
            for (int i = 0; i < lines.Length; i++)
            {
                string Key = lines[i].Split(delimiterChar)[0].Replace(":", "");
                double Value = Convert.ToDouble(lines[i].Split(delimiterChar)[1].Replace('.', ','));
                dict1.Add(Key, Value);
            }
            File.Delete(IMPORTANCE_MEASURES_PATH + IMPORTANCE_MEASURE_NAME);

            var sortedDict = (from entry in dict1 orderby entry.Value descending select entry).ToDictionary(entry => entry.Key, entry => entry.Value);
            //Alle Variablen + Measures Rausschreiben
            using (StreamWriter file = new StreamWriter(IMPORTANCE_MEASURES_PATH + IMPORTANCE_MEASURE_NAME.Replace(".importance", ".txt")))
                foreach (var entry in sortedDict)
                    file.WriteLine("{0}" + "\t" + "{1}", entry.Key, entry.Value);

            var Variables = (from entry in dict1 orderby entry.Value descending select entry.Key).Take(AnzahlHerausziehen).ToList();
            return Variables;
        }

        private void /*string[]*/ Select_EINSCHLÜSSE(StreamReader SourceStreamReader, StreamWriter TargetStreamWriter, List<string> einschlüsse, int NumRows)
        {
            SourceStreamReader.DiscardBufferedData();
            SourceStreamReader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);

            string FirstLineStringKomma = SourceStreamReader.ReadLine();
            string[] FirstLine = FirstLineStringKomma.Split(delimiter);
            int NumOfCol = FirstLine.Length;

            SourceStreamReader.DiscardBufferedData();
            SourceStreamReader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);

            //string[] firstLine = input[0].Split(delimiter);
            //string[] output = new string[input.Length];
            //int NumOfCol = firstLine.Length;
            //Reihenweise
            for (int i = 0; i < NumRows; i++)
            {
                //string[] currentInputLine = input[i].Split(delimiter);

                string s3 = SourceStreamReader.ReadLine();
                string[] currentInputLine = s3.Split(delimiter);

                string[] currentOutputLine = new string[einschlüsse.Count];
                int OutputIndex = 0;
                for (int a = 0; a < NumOfCol; a++)
                {
                    if (einschlüsse.IndexOf(FirstLine[a]) != -1)
                    {
                        currentOutputLine[OutputIndex] = currentInputLine[a];
                        OutputIndex++;
                    }

                }
                //output[i] = String.Join("\t", currentOutputLine);
                TargetStreamWriter.WriteLine(String.Join("\t", currentOutputLine));
            }
            TargetStreamWriter.Flush();
            //return output;
        }
        private void /*string[]*/ InputVorbereiten(StreamReader SourceStreamReader, List<string> ausschlüsse, StreamWriter BufferStreamWriter, int NumRows)
        {
            /* List<string> FullList = input[0].Split(delimiter).ToList();
             List<string> einschlüsse = FullList.Except(ausschlüsse).ToList(); */
            /*string[] output = */
            Select_AUSSCHLÜSSE(SourceStreamReader, ausschlüsse, BufferStreamWriter, NumRows);

            /* return output; */
        }
        private void /*string[]*/ Select_AUSSCHLÜSSE(StreamReader SourceStreamReader, List<string> ausschlüsse, StreamWriter BufferStreamWriter, int NumRows)
        {
            //ZURÜCKSETZEN
            SourceStreamReader.DiscardBufferedData();
            SourceStreamReader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
            string FirstLineStringKomma = SourceStreamReader.ReadLine();
            string[] FirstLine = FirstLineStringKomma.Split(delimiter);
            SourceStreamReader.DiscardBufferedData();
            SourceStreamReader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
            //string[] firstLine = input[0].Split(delimiter);
            //string[] output = new string[input.Length];
            int NumOfCol = FirstLine.Length;
            for (int i = 0; i < NumRows; i++)
            {
                string s3 = SourceStreamReader.ReadLine();
                string[] currentInputLine = s3.Split(delimiter);
                //string[] currentOutputLine = new string[150];
                string[] currentOutputLine = new string[NumOfCol];
                int OutputIndex = 0;
                for (int a = 0; a < NumOfCol; a++)
                {
                    if (ausschlüsse.IndexOf(FirstLine[a]) == -1)
                    {
                        if (currentInputLine[a].Contains(","))
                            throw new Exception("Komma enthalten");
                        currentOutputLine[OutputIndex] = currentInputLine[a];
                        OutputIndex++;
                    }

                }
                BufferStreamWriter.WriteLine(String.Join("\t", currentOutputLine));
                //output[i] = String.Join("\t", currentOutputLine);
            }
            BufferStreamWriter.Flush();
            //return output;
        }
        int TotalLines(string filePath)
        {
            using (StreamReader r = new StreamReader(filePath))
            {
                int i = 0;
                while (r.ReadLine() != null) { i++; }
                return i;
            }
        }
#else
        public void StartRanger(Program p)
        {
            string[] fileEntries = Directory.GetFiles(p.INPUT_DATA_PATH, "*" /*, SearchOption.AllDirectories */);
            //fileEntries = fileEntries.Where(x => !x.Contains("_W")).ToArray();
            fileEntries = fileEntries.Where(x => !(x.Substring(x.Length - 6) == "_W.txt")).ToArray();

            foreach (string element in fileEntries)
            {
                string directory = element;
                string[] input = System.IO.File.ReadAllLines(directory);
                p.FILENAME = directory.Replace(p.INPUT_DATA_PATH, "").Replace(".txt", "");

                p.TARGET = input[0].Split(p.delimiter)[0];
                p.NOAK_NAME_TABLE = input[0].Split(p.delimiter)[1];
                string NOAK_NAME = input[0].Split(p.delimiter)[1].Replace("B_N_", "");
                //p.IMPORTANCE_MEASURE_NAME = "ICD_" + p.TARGET.Replace("B_T_", "") + "_" + "N_" + NOAK_NAME + "_" + "IMPORTANCE_MEASURE.importance";
                p.IMPORTANCE_MEASURE_NAME = "ICD_" + p.TARGET.Replace("B_T_", "") + "_" + "N_" + NOAK_NAME + "_" + "IMPORTANCE_MEASURE.txt";
                p.Steuerung(input);
            }
        }
        public void Steuerung(string[] input)
        {
            //TO DO:
            //KATEGORIALE VARIABLEN BEIM RANGEN SPEZIFIZIEREN


            //INPUT DATA reduzieren um I_Variablen
            string[] work = new string[input.Length];
            Array.Copy(input, work, input.Length);
            List<string> ausschlüsse = new List<string>();
            ausschlüsse.Add("W_KOMBI_KORREKTUR_FAKTOR");
            ausschlüsse.Add("I_VERSNR");
            work = InputVorbereiten(work, ausschlüsse); //Korrektur und Versnr rausschmeißen, später kategoriale Ändern

            //Zwischentabellen Rausschreiben
            System.IO.File.WriteAllLines(BUFFER_DATA_PATH + FILENAME + ".txt", work);

            ExecuteCommand(FILENAME, TARGET);

            File.Delete(BUFFER_DATA_PATH + FILENAME + ".txt");
            //Vom Ranger selektierte Variablen
            List<string> SelectedVariables = Sortieren();

            //SCHREIBE DIE DATEI NUR WENN NOAK SELEKTIERT WURDE
            if(SelectedVariables.IndexOf(NOAK_NAME_TABLE) != -1)
            {
                //Wieder Hinzufügen des Targets und der I_Variablen
                SelectedVariables.Add(TARGET);
                SelectedVariables.Add("W_KOMBI_KORREKTUR_FAKTOR");
                SelectedVariables.Add("I_VERSNR");

                //Wegschreiben der Tabellen mit 100 wichtigsten Variablen
                string[] output = Select_EINSCHLÜSSE(input, SelectedVariables);
                //if (output[0].Contains("I_KORREKTUR_FAKTOR"))
                //{
                //    output[0].Replace("I_KORREKTUR_FAKTOR", "C_KORREKTUR_FAKTOR");
                //}
                
                System.IO.File.WriteAllLines(OUTPUT_DATA_PATH + FILENAME + ".txt", output);
            }

        }

        public void ExecuteCommand(string Inputpath_Buffer, string NOAK_Name)
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                RedirectStandardInput = true
            };
            System.Diagnostics.Process proc = new System.Diagnostics.Process() { StartInfo = psi };

            proc.Start();

            proc.StandardInput.Write(CommandScript_Ranger(FILENAME, NOAK_Name));

            proc.WaitForExit();
            proc.Close();
        }
        private string CommandScript_Ranger(string FILENAME, string NOAK_Name)
        {
            
            StringBuilder sb = new StringBuilder();
            // VERZEICHNIS SETZEN
            sb.Append(@"cd " + StartupPath + @"\..\..\..\..\.." + Environment.NewLine);
            sb.Append(@"ranger64 --verbose --file .\3_RANGER_BUFFER_DATA\" + FILENAME + ".txt");
            sb.Append(@" --depvarname " + NOAK_Name);
            sb.Append(@" --treetype 1 --nthreads 4");
            sb.Append(@" --impmeasure 5"); /* --impmeasure 1*/ /* 1 <- GINI, 2 <- Permutation Importance 5 <- die korrigierte Impurity Importance (5) ist deutlich schneller*/
#if RangerGewichtung
            sb.Append(@" --caseweights .\2_OUTPUT_SPLIT_WEIGHTCREATION\" + FILENAME + "_W.txt");
#endif
            sb.Append(Environment.NewLine);

            //UMBENNEN
            sb.Append(@"rename ranger_out.importance " + IMPORTANCE_MEASURE_NAME + Environment.NewLine);

            //VERSCHIEBEN
            sb.Append(@"move /Y " + IMPORTANCE_MEASURE_NAME + @" .\" + "4_RANGER_IMPORTANCE_MEASURES" + Environment.NewLine);

            sb.Append("Exit" + Environment.NewLine);
            return sb.ToString();
        }
        private List<string> Sortieren()
        {  
            string[] lines = System.IO.File.ReadAllLines(IMPORTANCE_MEASURES_PATH + IMPORTANCE_MEASURE_NAME);

            Dictionary<string, double> dict1 = new Dictionary<string, double>();

            for (int i = 0; i < lines.Length; i++)
            {
                                //FFA 28.08 GEÄNDERT
                                //string Key = lines[i].Split(' ')[0].Replace(":", "");
                                //double Value = Convert.ToDouble(lines[i].Split(' ')[1].Replace('.',','));
                string Key = lines[i].Split(' ')[0].Replace(":", "");
                double Value = Convert.ToDouble(lines[i].Split(' ')[1].Replace('.',','));
                //string Key = lines[i].Split('\t')[0].Replace(":", "");
                //double Value = Convert.ToDouble(lines[i].Split('\t')[1].Replace('.', ','));
                dict1.Add(Key, Value);
            }
            File.Delete(IMPORTANCE_MEASURES_PATH + IMPORTANCE_MEASURE_NAME);

            var sortedDict = (from entry in dict1 orderby entry.Value descending select entry).ToDictionary(entry => entry.Key, entry => entry.Value);
            //Alle Variablen + Measures Rausschreiben
            using (StreamWriter file = new StreamWriter(IMPORTANCE_MEASURES_PATH + IMPORTANCE_MEASURE_NAME.Replace(".importance", ".txt")))
                foreach (var entry in sortedDict)
                    file.WriteLine("{0}" + "\t" + "{1}", entry.Key, entry.Value);

            var Variables = (from entry in dict1 orderby entry.Value descending select entry.Key).Take(AnzahlHerausziehen).ToList();
            return Variables;
        }

        private string[] Select_EINSCHLÜSSE(string[] input, List<string> einschlüsse)
        {
            string[] firstLine = input[0].Split(delimiter);
            string[] output = new string[input.Length];
            int NumOfCol = firstLine.Length;
            //Reihenweise
            for (int i = 0; i < input.Length; i++)
            {
                string[] currentInputLine = input[i].Split(delimiter);
                string[] currentOutputLine = new string[einschlüsse.Count];
                int OutputIndex = 0;
                for (int a = 0; a < NumOfCol; a++)
                {
                    if (einschlüsse.IndexOf(firstLine[a]) != -1)
                    {
                        currentOutputLine[OutputIndex] = currentInputLine[a];
                        OutputIndex++;
                    }
                    
                }
                output[i] = String.Join("\t", currentOutputLine);
            }

            return output;
        }
        private string[] Select_AUSSCHLÜSSE(string[] input, List<string> ausschlüsse)
        {
            string[] firstLine = input[0].Split(delimiter);
            string[] output = new string[input.Length];
            int NumOfCol = firstLine.Length;
            for (int i = 0; i < input.Length; i++)
            {
                string[] currentInputLine = input[i].Split(delimiter);
                //string[] currentOutputLine = new string[150];
                string[] currentOutputLine = new string[NumOfCol];
                int OutputIndex = 0;
                for (int a = 0; a < NumOfCol; a++)
                {
                    if (ausschlüsse.IndexOf(firstLine[a]) == -1)
                    {
                        currentOutputLine[OutputIndex] = currentInputLine[a];
                        OutputIndex++;
                    }

                }
                output[i] = String.Join("\t", currentOutputLine);
            }

            return output;
        }
        private string[] InputVorbereiten(string[] input, List<string> ausschlüsse)
        {
           /* List<string> FullList = input[0].Split(delimiter).ToList();
            List<string> einschlüsse = FullList.Except(ausschlüsse).ToList(); */
            string[] output = Select_AUSSCHLÜSSE(input, ausschlüsse);

            return output;
        }
#endif
    }
}
