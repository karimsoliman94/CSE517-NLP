﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace hw2
{
    internal class MainClass
    {
        public static string unkWord = "*UNK*";
        public static string startWord = "*START*";
        public static string stopWord = "*STOP*";
        public static int unkCount = 2;
        public static double lambda1 = 0.01;
        public static double lambda2 = 0.99;
        public static double k = 1;
        public static double sentenceCount=0;


        public static Dictionary<string, int> wordSet;
        public static void Main(string[] args)
        {

            Dictionary<String, Dictionary<String, double>> transitionCounts = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<String, Dictionary<String, double>> emissionCounts = new Dictionary<string, Dictionary<string, double>>();
            HashSet<string> stateSet = new HashSet<string>();


            var trainDataPath = @"../../CSE517_HW_HMM_Data/twt.train.json";
            var filteredTraininDataPath = @"../../CSE517_HW_HMM_Data/twt.train.filtered.json";
            wordSet = handleUnkown(trainDataPath, filteredTraininDataPath, unkCount);

            //read train data

            using (StreamReader reader = new StreamReader(filteredTraininDataPath))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var obj = JsonConvert.DeserializeObject<List<List<string>>>(line);
                    obj.Insert(0, new List<string> { startWord, startWord });
                    for (int i = 0; i < obj.Count - 1; i++)
                    {

                        var word = obj[i][0];
                        var tag = obj[i][1];
                        var nextTag = obj[i + 1][1];

                        stateSet.Add(tag);

                        //emission
                        if (emissionCounts.ContainsKey(tag))
                        {
                            var tagValues = emissionCounts[tag];
                            if (tagValues.ContainsKey(word))
                            {
                                tagValues[word]++;
                            }
                            else
                            {
                                tagValues.Add(word, 1);
                            }
                        }
                        else
                        {
                            var wordDictionary = new Dictionary<string, double>();
                            wordDictionary.Add(word, 1);
                            emissionCounts.Add(tag, wordDictionary);
                        }

                        //transition

                        if (transitionCounts.ContainsKey(tag))
                        {
                            var tagValues = transitionCounts[tag];
                            if (tagValues.ContainsKey(nextTag))
                            {
                                tagValues[nextTag]++;
                            }
                            else
                            {
                                tagValues.Add(nextTag, 1);
                            }
                        }
                        else
                        {
                            var tagDictionary = new Dictionary<string, double>();
                            tagDictionary.Add(nextTag, 1);
                            transitionCounts.Add(tag, tagDictionary);
                        }

                    }
                    //handle last on in list
                    var lastWord = obj[obj.Count - 1][0];
                    var lastTag = obj[obj.Count - 1][1];
                    if (emissionCounts.ContainsKey(lastTag))
                    {
                        var tagValues = emissionCounts[lastTag];
                        if (tagValues.ContainsKey(lastWord))
                        {
                            tagValues[lastWord]++;
                        }
                        else
                        {
                            tagValues.Add(lastWord, 1);
                        }
                    }
                    else
                    {
                        var wordDictionary = new Dictionary<string, double>();
                        wordDictionary.Add(lastWord, 1);
                        emissionCounts.Add(lastTag, wordDictionary);
                    }

                    //transition
                    if (transitionCounts.ContainsKey(lastTag))
                    {
                        var tagValues = transitionCounts[lastTag];
                        if (tagValues.ContainsKey(stopWord))
                        {
                            tagValues[stopWord]++;
                        }
                        else
                        {
                            tagValues.Add(stopWord, 1);
                        }
                    }
                    else
                    {
                        var tagDictionary = new Dictionary<string, double>();
                        tagDictionary.Add(stopWord, 1);
                        transitionCounts.Add(lastTag, tagDictionary);
                    }

                    //add stop
                    sentenceCount++;
                }
            }
            Console.WriteLine(emissionCounts.Count);
            Console.WriteLine(transitionCounts.Count);

            stateSet.Remove(startWord);

            //smooth emissions add -k 
            var newEmission =new Dictionary<String, Dictionary<String, double>>();
            foreach(var tag in emissionCounts)
            {
                if(tag.Value.ContainsKey(unkWord))
                {
                    newEmission.Add(tag.Key, tag.Value);
                    continue;
                }
                else
                {
                    var temp = new Dictionary<string, double>();
                    foreach (var emission in tag.Value)
                    {
                        temp.Add(emission.Key, emission.Value + k);
                    }
                    temp.Add(unkWord, k);
                    newEmission.Add(tag.Key,temp);
                }

            }

            //start the vetrbi 
            forwardViterbi(trainDataPath, transitionCounts, newEmission, stateSet);

        }

        public static Dictionary<string, int> handleUnkown(string dataPath, string outputPath, int unk)
        {
            Dictionary<string, int> wordSetL = new Dictionary<string, int>();

            using (StreamReader reader = new StreamReader(dataPath))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var obj = JsonConvert.DeserializeObject<List<List<string>>>(line);
                    foreach (var pair in obj)
                    {
                        if (wordSetL.ContainsKey(pair[0]))
                        {
                            wordSetL[pair[0]] += 1;
                        }
                        else
                        {
                            wordSetL.Add(pair[0], 1);
                        }
                    }
                }
            }
            Dictionary<string, int> filteredWordSet = wordSetL.Where(x => (x.Value >= unk)).ToDictionary(x => x.Key, x => x.Value);
            filteredWordSet.Add(unkWord, 0);


            using (StreamReader reader = new StreamReader(dataPath))
            {
                using (StreamWriter writer = new StreamWriter(outputPath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var obj = JsonConvert.DeserializeObject<List<List<string>>>(line);
                        foreach (var pair in obj)
                        {
                            if (!filteredWordSet.ContainsKey(pair[0]))
                            {
                                pair[0] = unkWord;
                                filteredWordSet[unkWord]++;
                            }
                        }
                        var seralized = JsonConvert.SerializeObject(obj);
                        writer.WriteLine(seralized);
                    }
                }

            }
            return filteredWordSet;
        }

        public static void forwardViterbi(string dataSet, Dictionary<String, Dictionary<String, double>> transitionCounts, Dictionary<String, Dictionary<String, double>> emissionCounts, HashSet<string> stateSet)
        {
            using (StreamReader reader = new StreamReader(dataSet))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var dataObject = JsonConvert.DeserializeObject<List<List<string>>>(line);
                    var bestScore = new Dictionary<string, double>();
                    var bestTag = new Dictionary<string, string>();

                    //first word 
                    int i = 0;
                    foreach (var tag in stateSet)
                    {
                        var key = "" + i + " " + tag;
                        var word = wordSet.ContainsKey(dataObject[i][0]) ? dataObject[i][0] : unkWord;
                        double prob = logProbTransition(transitionCounts, startWord, tag) + logProbEmission(emissionCounts, tag, word);
                        bestScore.Add(key, prob);
                        bestTag.Add(key, startWord);
                    }

                    //middle
                    for (i = 1; i < dataObject.Count; i++)
                    {
                        var word = wordSet.ContainsKey(dataObject[i][0]) ? dataObject[i][0] : unkWord;
                        foreach (var tag in stateSet)
                        {
                            foreach (var prevTag in stateSet)
                            {
                                var bestPreviousKey = "" + (i - 1) + " " + prevTag;
                                var bestPreviousValue = bestScore[bestPreviousKey];
                                var key = "" + i + " " + tag;
                                var logprob = bestPreviousValue + logProbTransition(transitionCounts, prevTag, tag) + logProbEmission(emissionCounts, tag, word);

                                if (bestScore.ContainsKey(key))
                                {
                                    var currentlogprob = bestScore[key];
                                    if (logprob > currentlogprob)
                                    {
                                        bestScore[key] = logprob;
                                        bestTag[key] = bestPreviousKey;
                                    }
                                }
                                else
                                {
                                    bestScore.Add(key, logprob);
                                    bestTag.Add(key, bestPreviousKey);
                                }
                            }
                        }
                    }

                    //last one

                    foreach (var prevTag in stateSet)
                    {
                        var bestPreviousKey = "" + (i - 1) + " " + prevTag;
                        var bestPreviousValue = bestScore[bestPreviousKey];
                        var key = "" + i + " " + stopWord;
                        var logprob = bestPreviousValue + logProbTransition(transitionCounts, prevTag, stopWord) + logProbEmission(emissionCounts, stopWord, stopWord);

                        if (bestScore.ContainsKey(key))
                        {
                            var currentlogprob = bestScore[key];
                            if (logprob > currentlogprob)
                            {
                                bestScore[key] = logprob;
                                bestTag[key] = bestPreviousKey;
                            }
                        }
                        else
                        {
                            bestScore.Add(key, logprob);
                            bestTag.Add(key, bestPreviousKey);
                        }
                    }

                    //backtrack and give tags

                    List<string> tags = new List<string>();

                    var currentTag = i+" "+stopWord;
                    while (!currentTag.Equals(startWord))
                    {
                        tags.Insert(0, bestTag[currentTag]);
                        currentTag = bestTag[currentTag];
                    }

                }
            }
        }

        public static double logProbTransition(Dictionary<String, Dictionary<String, double>> dict, string first, string second)
        {
            //bigram
            double numerator = dict[first].ContainsKey(second) ? dict[first][second] : 0;
            double denominator = dict[first].Sum(x => x.Value);
            double logdivision;
            if (numerator==0)
            {
                logdivision = double.MinValue;
            }
            else{
                double lognm = Math.Log(numerator);
                double logdm = Math.Log(denominator);
                logdivision = lognm - logdm;
            }


            //unigram
            double uniNumerator;
            if (second==stopWord)
            {
                uniNumerator = sentenceCount;//times we have seen a stop
            }
            else{
                uniNumerator = dict[second].Sum(x => x.Value);
            }
            double uniDenominator = dict.Sum(x => x.Value.Sum(y => y.Value));
            double uninmlog = Math.Log(uniNumerator);
            double unidmlog = Math.Log(uniDenominator);
            double uniDivision = uninmlog - unidmlog;


            return lambda2 * logdivision + lambda1 * uniDivision;
        }

        public static double logProbEmission(Dictionary<String, Dictionary<String, double>> dict, string first, string second)
        {
            if(first==stopWord&&second==stopWord)
            {
                return 0;
            }
            //bigram
            double numerator = dict[first].ContainsKey(second) ? dict[first][second] : dict[first][unkWord];
            double denominator =dict[first].Sum(x => x.Value) ;
            var lognm = Math.Log(numerator);
            var logdm = Math.Log(denominator);
            double logdivision =  lognm - logdm;

            ////signle
            //var singleNum = wordSet.ContainsKey(second) ? wordSet[second]: wordSet[unkWord];
            //var sum = wordSet.Sum(x => x.Value);
            //var singleDivision = singleNum / sum;
            //var singleLog = division == 0 ? 0 : Math.Log(division);

            //return lambda2 * log + lambda1 * singleLog;

            return logdivision;
        }
    }
}
