using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    public class Address
    {
        public string Mahalle { get; set; }
        public string Sokak { get; set; }
        public string Cadde { get; set; }
        public string Site { get; set; }
        public string Apt { get; set; }
        public string Bulv { get; set; }
        public string PostaKodu { get; set; }
        public string Il { get; set; }
        public string Ilce { get; set; }
        public string Semt { get; set; }
        public string No { get; set; }
        public string Kat { get; set; }
        public string Daire { get; set; }
    }

    public enum SuggestionType
    {
        City,
        District,
        County
    }

    public class Suggestion
    {
        public string SuggestedWord { get; set; }
        public SuggestionType SuggestedType { get; set; }
    }
}
