using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuiFunctions
{
    internal static class ClassExtensions
    {
        public static string ToCsvString(this DataTable table)
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
