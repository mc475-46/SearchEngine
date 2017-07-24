using System;
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

            var targetFiles = Directory.GetFiles(@"..\..\data\select10", @"*.txt");
            Parallel.ForEach(targetFiles, fileName =>
            {
                Console.WriteLine("Processing " + fileName);

                var wordList = new Dictionary<string, int>(); // 単語数カウント用リスト

                int wordCount = 0;

                MeCabParam param = new MeCabParam();
                param.DicDir = @"..\..\lib\dic\ipadic";
                MeCabTagger t = MeCabTagger.Create(param);

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

                            var originalForm = (node.Feature.Split(',')[6] == null || node.Feature.Split(',')[6] == "" || node.Feature.Split(',')[6] == "*") ? node.Surface : node.Feature.Split(',')[6];
                            // 原形がないものは表装文字を代表とし、原形がある場合はそちらを代表とする

                            if (!wordList.ContainsKey(originalForm))
                            {
                                wordList[originalForm] = 0;
                            }
                            ++wordList[originalForm];
                        }
                        node = node.Next;
                    }
                }

                Parallel.ForEach(wordList.Keys, word =>
                {
                    lock (wordList)
                    {
                        if (!weightList.ContainsKey(word)) weightList[word] = new Dictionary<string, double>();
                        weightList[word][fileName] = wordList[word] / (double)wordCount;
                    }
                });

            });

            Console.WriteLine("Calculating Inverse Document Frequency and Recording Weight to weightList ...");
            Parallel.For(0, weightList.Keys.Count, i =>
            {
                var word = weightList.Keys.ToArray()[i];
                Parallel.For(0, weightList[word].Keys.Count, j =>
                {
                    var fileName = weightList[word].Keys.ToArray()[j];
                    weightList[word][fileName] *= Math.Log((double)targetFiles.Length / weightList[word].Count, 2) + 1;
                });
            });

            Console.WriteLine("Constructing Inverted Index ...");
            Parallel.ForEach(weightList.Keys, word =>
            {
                invertedIndex[word] = weightList[word].Keys.OrderByDescending(fileName => weightList[word][fileName]).ThenBy(fileName => fileName).ToList();
                if (!invertedIndex.ContainsKey(word))
                {
                    Console.WriteLine($"{word}は転置インデックスに含まれていません");
                }
                if (word == null)
                {
                    Console.WriteLine("単語が空です");
                }
            });

            foreach(var hoge in invertedIndex.Keys)
            {
                Console.Write($"{hoge}\t");
                foreach(var fuga in invertedIndex[hoge])
                {
                    Console.Write($"{fuga} ");
                }
                Console.WriteLine();
            }
            
            Console.WriteLine("Successfully constructing Inverted Index");

            Console.Read();
        }
    }
}
