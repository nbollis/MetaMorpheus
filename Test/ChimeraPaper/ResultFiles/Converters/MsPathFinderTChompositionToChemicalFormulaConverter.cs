using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Test.ChimeraPaper.ResultFiles
{
    /// <summary>
    /// Converts the chemical formula from MsPathFinderT to MetaMorpheus
    /// MsPathFinderT: "C(460) H(740) N(136) O(146) S(0)"
    /// MetaMorpheus: "C460H740N136O146S"
    /// </summary>
    public class MsPathFinderTCompositionToChemicalFormulaConverter : DefaultTypeConverter
    {
        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            var composition = text.Split(' ').Where(p => p != "").ToArray();
            var chemicalFormula = new Chemistry.ChemicalFormula();
            foreach (var element in composition)
            {
                var elementSplit = element.Split('(');
                var elementName = elementSplit[0];
                var elementCount = int.Parse(elementSplit[1].Replace(")", ""));
                chemicalFormula.Add(elementName, elementCount);
            }
            return chemicalFormula;
        }

        public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            var chemicalFormula = value as Chemistry.ChemicalFormula ?? throw new Exception("Cannot convert input to ChemicalFormula");
            var sb = new StringBuilder();

            bool onNumber = false;
            foreach (var character in chemicalFormula.Formula)
            {
                if (!char.IsDigit(character)) // if is a letter
                {
                    if (onNumber)
                    {
                        sb.Append(") " + character);
                        onNumber = false;
                    }
                    else
                        sb.Append(character);
                }
                else
                {
                    if (!onNumber)
                    {
                        sb.Append("(" + character);
                        onNumber = true;
                    }
                    else
                        sb.Append(character);
                }
            }

            var stringForm = sb.ToString();
            if (char.IsDigit(stringForm.Last()))
                stringForm += ")";
            else
                stringForm += "(1)";

            return stringForm;
        }
    }
}
