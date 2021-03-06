﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using peng04.logic;
using peng04.processing;

namespace peng04.experiments
{
    public class ExperimentOne
    {
        public void Start(ILanguageProcessor processor, ISmoothingTechnique smoothing)
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var bayesClassifier = new BayesTextClassifier();

            var docReader = new ReadDocumentFromXmlFile();
            var docPath = Path.Combine(baseDirectory, "data");
            var authors = new DirectoryInfo(docPath).GetDirectories();

            var categories = new List<TextSource>();

            // Prepare data
            foreach (var item in authors)
            {
                var docs = item.GetFiles();
                var dataSource = new List<string>();

                foreach (var doc in docs)
                {
                    try
                    {
                        dataSource.Add(docReader.ReadDocumentText(doc.FullName, Encoding.GetEncoding(1253), new CultureInfo("el-GR")));
                    }
                    catch
                    {
                        Console.WriteLine("Document {0} unreadable", doc.FullName);
                    }
                }

                categories.Add(processor.Process(dataSource, item.Name));
            }

            Console.WriteLine("Scanned {1} documents in {0} categories", categories.Count, categories.Select(el => el.Documents.Count).Aggregate((el1, el2) => el1 + el2));

            var testPath = Path.Combine(baseDirectory, "test");
            var testAuthors = new DirectoryInfo(testPath).GetDirectories();
            var allInOne = new TextSource();
            allInOne.Documents.AddRange(categories.SelectMany(el => el.Documents));

            // choose n from 1 to 4
            for (int n = 1; n <= 4; n++)
            {
                Console.WriteLine("-----PREPARE for n = {0}", n);
                Console.WriteLine("Building hash tables ..", n);

                Parallel.ForEach(categories, category =>
                {
                    category.BuildSegmentTable(n);
                });

                allInOne.SetNGramCache(NGramCache.Aggregate(categories.Select(el => el.GetNGramCache())));

                Console.WriteLine("Getting smoothing ready ..");
                smoothing.Init(allInOne, n);
                var categoriesToTest = new Dictionary<TextSource, CategoryProbabilityDistribution>();

                foreach(var cat in categories)
                {
                    categoriesToTest[cat] = new CategoryProbabilityDistribution(cat, smoothing, n);
                }

                int rightClassified = 0;
                int wrongClassified = 0;

                Console.WriteLine("-----Algorithm starts now");
                foreach (var testAuthor in testAuthors)
                {
                    foreach (var testDocument in testAuthor.GetFiles())
                    {
                        TextSource topCategory = null;
                        var maxProb = 0.0;

                       Parallel.ForEach(categoriesToTest, catDist =>
                       {
                           var docText = new[] { docReader.ReadDocumentText(testDocument.FullName, Encoding.GetEncoding(1253), new CultureInfo("el-GR")) };
                           var docSource = processor.Process(docText, testAuthor.Name).Documents.First();

                           double p = bayesClassifier.P_c(catDist.Value, docSource, n, 1.0 / (double)categories.Count);

                           if (topCategory == null || p > maxProb)
                           {
                               topCategory = catDist.Key;
                               maxProb = p;
                           }
                       });

                        Console.WriteLine("Classified {0} as author {1} - {2}", testDocument.Name, topCategory.Name, topCategory.Name == testAuthor.Name ? "correct" : "incorrect");

                        if (topCategory.Name == testAuthor.Name) rightClassified++;
                        else wrongClassified++;
                    }
                }

                Console.WriteLine("-----SUMMARY");
                Console.WriteLine("Success rate for n={0} is {1}\n", n, (double)rightClassified / ((double)rightClassified + (double)wrongClassified));
            }
        }
    }
}