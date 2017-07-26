using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NMeCab;

namespace SearchEngine
{
    class Program
    {
        static void Main(string[] args)
        {
            var weightList = new Dictionary<string, Dictionary<string, double>>(); // Dictionary<word, Dictionary<filename, weight>>
            var invertedIndex = new Dictionary<string, List<string>>(); // Dictionary<word, List<filename orderby weight>>


            Console.WriteLine("Calculating Term Frequency ...");

            var targetFiles = Directory.GetFiles(@"..\..\data\select10000", @"*.txt");

            MeCabParam param = new MeCabParam();
            param.DicDir = @"..\..\lib\dic\ipadic";
            MeCabTagger t = MeCabTagger.Create(param);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            {
                Parallel.ForEach(targetFiles, fileName =>
                {
                    Console.WriteLine("Processing " + fileName);

                    var wordList = new Dictionary<string, int>(); // 単語数カウント用リスト

                    int wordCount = 0;
                    var lockObject = new Object();

                    Parallel.ForEach(File.ReadLines(fileName), line =>
                    {
                        var node = t.ParseToNode(line);
                        while (node != null)
                        {
                            if (node.CharType > 0)
                            {
                                lock (lockObject)
                                {
                                    ++wordCount;
                                }

                                var normalized = node.Feature.Split(',')[6];
                                var originalForm = (normalized == null || normalized == "" || normalized == "*") ? node.Surface : normalized;
                                // 原形がないものは表装文字を代表とし、原形がある場合はそちらを代表とする

                                lock (wordList)
                                {
                                    if (!wordList.ContainsKey(originalForm))
                                    {
                                        wordList[originalForm] = 0;
                                    }
                                    ++wordList[originalForm];
                                }
                            }
                            node = node.Next;
                        }
                    });
                    /*
                    var file = new StreamReader(fileName);
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        var node = t.ParseToNode(line);
                        while (node != null)
                        {
                            if (node.CharType > 0)
                            {
                                ++wordCount;

                                var normalized = node.Feature.Split(',')[6];
                                var originalForm = (normalized == null || normalized == "" || normalized == "*") ? node.Surface : normalized;
                                // 原形がないものは表装文字を代表とし、原形がある場合はそちらを代表とする

                                if (!wordList.ContainsKey(originalForm))
                                {
                                    wordList[originalForm] = 0;
                                }
                                ++wordList[originalForm];
                            }
                            node = node.Next;
                        }
                    }*/

                    Parallel.ForEach(wordList.Keys, word =>
                    {
                        lock (weightList)
                        {
                            if (!weightList.ContainsKey(word)) weightList[word] = new Dictionary<string, double>();
                            weightList[word][fileName] = wordList[word] / (double)wordCount;
                        }
                    });

                });
            }
            sw.Stop();
            Console.WriteLine($"{sw.ElapsedMilliseconds} msec Elpsed.");

            Console.WriteLine("Constructing Inverted Index ...");
            sw.Restart();
            {
                /*
                invertedIndex = weightList.Keys
                    .AsParallel()
                    .ToDictionary(
                        word => word,
                        word => weightList[word].Keys
                            .OrderByDescending(fileName => weightList[word][fileName])
                            .ThenBy(fileName => fileName)
                            .ToList());
                */

                Parallel.ForEach(weightList.Keys, word =>
                {
                    var ks = weightList[word].Keys.OrderByDescending(fileName => weightList[word][fileName]).ThenBy(fileName => fileName).ToList();
                    lock (invertedIndex)
                    {
                        invertedIndex[word] = ks;
                    }

                    if (!invertedIndex.ContainsKey(word))
                    {
                        Console.WriteLine($"{word}は転置インデックスに含まれていません");
                    }
                    if (word == null)
                    {
                        Console.WriteLine("単語が空です");
                    }
                });
            }
            sw.Stop();
            Console.WriteLine($"{sw.ElapsedMilliseconds} msec Elpsed.");

            Console.WriteLine("Calculating Inverse Document Frequency and Recording Weight to weightList ...");

            sw.Restart();
            {
                
                weightList = weightList.AsParallel()
                    .ToDictionary(
                        kv1 => kv1.Key,
                        kv1 =>
                        {
                            var idf = Math.Log(targetFiles.Length / kv1.Value.Count, 2) + 1;
                            return kv1.Value.ToDictionary(kv2 => kv2.Key, kv2 => kv2.Value * idf);
                        });
            }
            sw.Stop();
            Console.WriteLine($"{sw.ElapsedMilliseconds} msec Elpsed.");

            StreamWriter writer = new StreamWriter(@"dump.txt", false, Encoding.GetEncoding("utf-8"));
            foreach (var word in invertedIndex.Keys)
            {
                writer.Write($"{word}\t");
                foreach (var filename in invertedIndex[word])
                {
                    writer.Write($"({filename}, {weightList[word][filename]}), ");
                }
                writer.WriteLine();
            }
            writer.Close();

            Console.WriteLine("Successfully finishing all procedures.");

            Console.Read();
        }
    }
}
