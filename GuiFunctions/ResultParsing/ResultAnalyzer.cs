using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuiFunctions
{
    public class ResultAnalyzer
    {
        protected static string[] ambiguityLevels = new string[] { "1", "2A", "2B", "2C", "2D", "3", "4", "5" };

        protected static string[] defaultColumns = new string[]
            { "Fraction", "Proteoforms", "Filtered Proteoforms", "Psms", "Filtered Psms" };

        public Dictionary<string, int> FileNameIndex { get; set; }
        public int MaxProteoformChimerasFromOneSpectra { get; set; } = 0;
        public int MaxPsmChimerasFromOneSpectra { get; set; } = 0;

        public static void AddDefaultColumnsToTable(DataTable table)
        {
            foreach (var columnType in defaultColumns)
            {
                Type type = typeof(double);
                switch (columnType)
                {
                    case "Fraction":
                        type = typeof(string);
                        break;

                    case "Proteoforms":
                    case "Filtered Proteoforms":
                    case "Psms":
                    case "Filtered Psms":
                        type = typeof(int);
                        break;
                }

                DataColumn column = new DataColumn
                {
                    DataType = type,
                    ColumnName = columnType.ToString(),
                    Caption = columnType.ToString(),
                };
                table.Columns.Add(column);
            }
        }

        /// <summary>
        /// Returns a string representation of all values in the table
        /// </summary>
        /// <returns></returns>
        public static string OutputDataTable(DataTable table)
        {
            string data = string.Empty;
            StringBuilder builder = new();
            foreach (var column in table.Columns)
            {
                builder.Append(column.ToString() + ",");
            }
            builder.AppendLine();

            foreach (DataRow row in table.Rows)
            {
                foreach (var item in row.ItemArray)
                {
                    builder.Append(item.ToString() + ",");
                }

                builder.AppendLine();
            }

            data = builder.ToString();
            return data;
        }
    }
}
