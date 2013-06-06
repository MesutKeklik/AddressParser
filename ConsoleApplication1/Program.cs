using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using SpellChecker.Net.Search.Spell;

namespace ConsoleApplication1
{
    class Program
    {
        enum SearchType
        {
            Mah,
            Sok,
            Apt,
            Cad,
            Site,
            Bulv,
            No,
            Kat,
            Daire
        }

        //sabit regex 
        public const string MahReg = "(( m[ ])|( m[. ])|( mh[ ])|( mh[. ])|( mah[ ])|( mah[. ])|( mahalle.*[ ]))";
        public const string SkReg = "(( s[ ])|( s[. ])|( sk[ ])|( sk[. ])|( sok[ ])|( sok[. ])|( sokak[ ])|( sokağ.*[ ]))";
        public const string AptReg = "(( a[ ])|( a[. ])|( ap[ ])|( ap[. ])|( apt[ ])|( apt[. ])|( apart.*[ ]))";
        public const string CadReg = "(( c[ ])|( c[. ])|( cd[ ])|( cd[. ])|( cad[ ])|( cad[. ])|( cadde.*[ ]))";
        public const string SiteReg = "(( st[ ])|( st[. ])|( site.*[ ]))";
        public const string BulvReg = "(( bl[ ])|( bl[. ])|( bulv.*[ ]))";
        public const string NoReg = "(( n[.])|( n[.:])|( n[:])|( no[.])|( no[.:])|( no[:]))";
        public const string KatReg = "(( k[.])|( k[.:])|( k[:])|( kat[.])|( kat[.:])|( kat[:]))";
        public const string DaireReg = "(( d[.])|( d[.:])|( d[:])|( da[.])|( da[.:])|( da[:])|( daire[:]))";

        static void Main()
        {
            const string addressStr = "Lale Ap.  GMK Bulvarı İnceyol sk. Küçükyalı Evleri st Merkez m.   No.36/1 K.1 Da:5 Küçükyalı Maltpe İstambl  ";
            ParseAddress(addressStr);
        }

        static void ParseAddress(string addressStr)
        {
            var tmpAddr = addressStr;

            Console.WriteLine("Düzensiz Adres:" + addressStr);

            //kurala uyan kelimeler
            var rulledMatches = "";
            const string tmpMatch = "";
            var wsSep = new[] { " " };

            var dict = GetSearchDict(addressStr);
            var orderedDict = dict.OrderBy(x => x.Key);

            var addr = new Address();
            
            //sıralanmış dictionary arama yapıp addr'nin ilgili alanlarının doldurulduğu kısım.
            foreach (var item in orderedDict)
            {
                var word = FindAddressPart(addressStr, rulledMatches, tmpMatch, item.Value);
                switch (item.Value)
                {
                    case SearchType.Mah:
                        addr.Mahalle = word[0];
                        break;
                    case SearchType.Sok:
                        addr.Sokak = word[0];
                        break;
                    case SearchType.Apt:
                        addr.Apt = word[0];
                        break;
                    case SearchType.Cad:
                        addr.Cadde = word[0];
                        break;
                    case SearchType.Site:
                        addr.Site = word[0];
                        break;
                    case SearchType.Bulv:
                        addr.Bulv = word[0];
                        break;
                    case SearchType.No:
                        addr.No = word[0];
                        break;
                    case SearchType.Kat:
                        addr.Kat = word[0];
                        break;
                    case SearchType.Daire:
                        addr.Daire = word[0];
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                rulledMatches = word[2];
                addressStr = word[1];
            }

            tmpAddr = rulledMatches.Split(wsSep, StringSplitOptions.RemoveEmptyEntries).Select(s => new Regex(Regex.Escape(s))).Aggregate(tmpAddr, (current, regex) => regex.Replace(current, "", 1));

            //kurallara uymayan il ilce semt posta kodu gibi tek kelimelik bilgiler ayiklaniyor
            var cityDistrict = tmpAddr.Split(wsSep, StringSplitOptions.RemoveEmptyEntries);

            //içinde özel karakter ve sayı yoksa il ilçe ya da semttir.
            var cityDistrictFinal = cityDistrict.Where(s => (!s.Contains(".")) && (!s.Contains(":")) && s.All(Char.IsLetter)).ToList();
            
            //sadece sayılardan oluşuyorsa ve uzunluğu da 5 ise posta kodudur.
            var postalCode = cityDistrict.Where(s => (!s.Contains(".")) && (!s.Contains(":")) && s.All(Char.IsDigit) && s.Length == 5).ToList();
            addr.PostaKodu = postalCode.Count > 0 ? postalCode[0] : "";

            foreach (var cityorDistrict in cityDistrictFinal)
            {
                var suggestion = SpellCheck(cityorDistrict.Trim());
                switch (suggestion.SuggestedType)
                {
                    case SuggestionType.City:
                        addr.Il = suggestion.SuggestedWord;
                        break;
                    case SuggestionType.District:
                        addr.Semt = suggestion.SuggestedWord;
                        break;
                    case SuggestionType.County:
                        addr.Ilce = suggestion.SuggestedWord;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            Console.WriteLine("Mahalle:" + addr.Mahalle);
            Console.WriteLine("Sokak:" + addr.Sokak);
            Console.WriteLine("Cadde:" + addr.Cadde);
            Console.WriteLine("Site:" + addr.Site);
            Console.WriteLine("Apt:" + addr.Apt);
            Console.WriteLine("Bulv:" + addr.Bulv);
            Console.WriteLine("No:" + addr.No);
            Console.WriteLine("Kat:" + addr.Kat);
            Console.WriteLine("Daire:" + addr.Daire);
            Console.WriteLine("Posta Kodu:" + addr.PostaKodu);
            Console.WriteLine("Semt:" + addr.Semt);
            Console.WriteLine("Ilçe:" + addr.Ilce);
            Console.WriteLine("Il:" + addr.Il);

            Console.ReadKey();
        }


        /// <summary>
        /// tek kelime seklinde girilmis bilgilerin dogrulugunu kontrol edip onerileni getirir
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        private static Suggestion SpellCheck(string word)
        {
            var dir = new RAMDirectory();
            var iw = new IndexWriter(dir, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), IndexWriter.MaxFieldLength.UNLIMITED);

            var distDoc = new Document();
            var textdistField = new Field("text", "", Field.Store.YES, Field.Index.ANALYZED);
            distDoc.Add(textdistField);
            var iddistField = new Field("id", "", Field.Store.YES, Field.Index.NOT_ANALYZED);
            distDoc.Add(iddistField);

            textdistField.SetValue("Küçükyalı Kozyatağı");
            iddistField.SetValue("0");

            var countyDoc = new Document();
            var textcountyField = new Field("text", "", Field.Store.YES, Field.Index.ANALYZED);
            countyDoc.Add(textcountyField);
            var idcountyField = new Field("id", "", Field.Store.YES, Field.Index.NOT_ANALYZED);
            countyDoc.Add(idcountyField);

            textcountyField.SetValue("Maltepe Maslak");
            idcountyField.SetValue("1");

            var cityDoc = new Document();
            var textcityField = new Field("text", "", Field.Store.YES, Field.Index.ANALYZED);
            cityDoc.Add(textcityField);
            var idcityField = new Field("id", "", Field.Store.YES, Field.Index.NOT_ANALYZED);
            cityDoc.Add(idcityField);

            textcityField.SetValue("İstanbul İzmir");
            idcityField.SetValue("2");

            iw.AddDocument(distDoc);
            iw.AddDocument(cityDoc);
            iw.AddDocument(countyDoc);

            iw.Commit();
            var reader = iw.GetReader();

            var speller = new SpellChecker.Net.Search.Spell.SpellChecker(new RAMDirectory());
            speller.IndexDictionary(new LuceneDictionary(reader, "text"));
            var suggestions = speller.SuggestSimilar(word, 5);

            var retVal = new Suggestion {SuggestedWord = suggestions.Length > 0 ? suggestions[0] : ""};

            var searcher = new IndexSearcher(reader);
            foreach (var doc in suggestions.Select(suggestion => searcher.Search(new TermQuery(new Term("text", suggestion)), null, Int32.MaxValue)).SelectMany(docs => docs.ScoreDocs))
            {
                switch (searcher.Doc(doc.Doc).Get("id"))
                {
                    case "0":
                        retVal.SuggestedType = SuggestionType.District;
                        break;
                    case "1":
                        retVal.SuggestedType = SuggestionType.County;
                        break;
                    case "2":
                        retVal.SuggestedType = SuggestionType.City;
                        break;
                }
            }

            reader.Dispose();
            iw.Dispose();

            return retVal;
        }


        /// <summary>
        /// Address belirtilen arama tipine göre gereken kısmı bulur
        /// </summary>
        /// <param name="addressStr"></param>
        /// <param name="rulledMatches"></param>
        /// <param name="tmpMatch"></param>
        /// <param name="sType"></param>
        /// <returns></returns>
        private static string[] FindAddressPart(string addressStr, string rulledMatches, string tmpMatch, SearchType sType)
        {
            int matchIndex = 0;
            bool changeIndex = false;
            if (tmpMatch == null) throw new ArgumentNullException("tmpMatch");
            var wsSep = new[] { " " };
            string regularExpression;
            switch (sType)
            {
                case SearchType.Mah:
                    regularExpression = MahReg;
                    break;
                case SearchType.Sok:
                    regularExpression = SkReg;
                    break;
                case SearchType.Apt:
                    regularExpression = AptReg;
                    break;
                case SearchType.Cad:
                    regularExpression = CadReg;
                    break;
                case SearchType.Site:
                    regularExpression = SiteReg;
                    break;
                case SearchType.Bulv:
                    regularExpression = BulvReg;
                    break;
                case SearchType.No:
                    regularExpression = NoReg;
                    changeIndex = true;
                    break;
                case SearchType.Kat:
                    regularExpression = KatReg;
                    changeIndex = true;
                    break;
                case SearchType.Daire:
                    regularExpression = DaireReg;
                    changeIndex = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("sType");
            }

            var matched = Regex.Matches(addressStr, regularExpression, RegexOptions.IgnoreCase);
            
            var match = Regex.Split(addressStr, regularExpression, RegexOptions.IgnoreCase);

            if (changeIndex)
                matchIndex = match.Length - 1;


            var repWord = match[matchIndex];

            if (addressStr.Length > repWord.Length)
            {
                
                if (changeIndex)
                {
                    var replaceWordArray = match[matchIndex].Split(wsSep, StringSplitOptions.RemoveEmptyEntries);
                    repWord = replaceWordArray[0];
                }

                addressStr = addressStr.Replace(repWord, "");
                rulledMatches += " " + repWord;
            }
            else
                repWord = "";
            if (matched.Count > 0)
            {
                tmpMatch = matched[0].Value.Split(' ').Length > 0
                               ? matched[0].Value.Split(wsSep, StringSplitOptions.RemoveEmptyEntries)[0]
                               : matched[0].Value;
                addressStr = addressStr.Replace(tmpMatch, "");
                rulledMatches += " " + tmpMatch;
            }

            return new[] { repWord.Trim(), addressStr, rulledMatches };
        }


        /// <summary>
        /// Adres içinde aramanın yapılacağı tiplerin adres içinde nerede geçtiğini döner
        /// </summary>
        /// <param name="addressStr"></param>
        /// <returns></returns>
        private static Dictionary<int, SearchType> GetSearchDict(string addressStr)
        {
            var matchPos = new Dictionary<int, SearchType>();

            var m = Regex.Matches(addressStr, MahReg, RegexOptions.IgnoreCase);
            var ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;

            if (ndx >0) matchPos.Add(ndx, SearchType.Mah);

            m = Regex.Matches(addressStr, SkReg, RegexOptions.IgnoreCase);
            ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;
            if (ndx > 0) matchPos.Add(ndx, SearchType.Sok);

            m = Regex.Matches(addressStr, AptReg, RegexOptions.IgnoreCase);
            ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;
            if (ndx > 0) matchPos.Add(ndx, SearchType.Apt);

            m = Regex.Matches(addressStr, CadReg, RegexOptions.IgnoreCase);
            ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;
            if (ndx > 0) matchPos.Add(ndx, SearchType.Cad);

            m = Regex.Matches(addressStr, SiteReg, RegexOptions.IgnoreCase);
            ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;
            if (ndx > 0) matchPos.Add(ndx, SearchType.Site);

            m = Regex.Matches(addressStr, BulvReg, RegexOptions.IgnoreCase);
            ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;
            if (ndx > 0) matchPos.Add(ndx, SearchType.Bulv);

            m = Regex.Matches(addressStr, NoReg, RegexOptions.IgnoreCase);
            ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;
            if (ndx > 0) matchPos.Add(ndx, SearchType.No);


            m = Regex.Matches(addressStr, KatReg, RegexOptions.IgnoreCase);
            ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;
            if (ndx > 0) matchPos.Add(ndx, SearchType.Kat);


            m = Regex.Matches(addressStr, DaireReg, RegexOptions.IgnoreCase);
            ndx = 0;
            if (m.Count > 0)
                ndx = m[0].Index;
            if (ndx > 0) matchPos.Add(ndx, SearchType.Daire);

            return matchPos;
        }

    }


}

