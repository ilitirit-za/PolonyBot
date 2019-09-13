using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace PolonyBot.Modules.LFG.Utils
{
    // Adapted from:
    // https://github.com/tarikguney/ascii-table-creator
    public class AsciiTableGenerator
    {
        public static int GetEstimatedTableSizeInCharacters(DataTable table)
        {
            var lengthByColumnDictionary = GetTotalSpaceForEachColumn(table);
            
            // Sum of Column Sizes + 3 chars per column for lines/spacing + 2 chars for \r\n
            var rowLength = lengthByColumnDictionary.Values.Sum() + (table.Columns.Count * 3) + 2;
            
            // Add 1 for header and one for line spacer
            return rowLength * (table.Rows.Count + 2);
        }

        public static StringBuilder CreateAsciiTableFromDataTable(DataTable table)
        {
            var lengthByColumnDictionary = GetTotalSpaceForEachColumn(table);
            
            var tableBuilder = new StringBuilder();
            AppendColumns(table, tableBuilder, lengthByColumnDictionary);
            AppendRows(table, lengthByColumnDictionary, tableBuilder);

            return tableBuilder;
        }

        private static void AppendRows(DataTable table, IReadOnlyDictionary<int, int> lenghtByColumnDictionary,
            StringBuilder tableBuilder)
        {
            for (var i = 0; i < table.Rows.Count; i++)
            {
                var rowBuilder = new StringBuilder();
                for (var j = 0; j < table.Columns.Count; j++)
                {
                    rowBuilder.Append(PadWithSpaceAndSeparator(table.Rows[i][j].ToString().Trim(),
                        lenghtByColumnDictionary[j]));
                }
                tableBuilder.AppendLine(rowBuilder.ToString());
            }
        }

        private static void AppendColumns(DataTable table, StringBuilder builder,
            IReadOnlyDictionary<int, int> lengthByColumnDictionary)
        {
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var columnName = table.Columns[i].ColumnName.Trim();
                var paddedColumnNames = PadWithSpaceAndSeparator(columnName, lengthByColumnDictionary[i]);
                builder.Append(paddedColumnNames);
            }
            builder.AppendLine();
            builder.AppendLine(string.Join("", Enumerable.Repeat("-", builder.ToString().Length - 3).ToArray()));
        }
        
        private static Dictionary<int, int> GetTotalSpaceForEachColumn(DataTable table)
        {
            var lengthByColumn = new Dictionary<int, int>();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var length = new int[table.Rows.Count];
                for (var j = 0; j < table.Rows.Count; j++)
                {
                    length[j] = table.Rows[j][i].ToString().Trim().Length;
                }
                lengthByColumn[i] = length.Max();
            }
            return CompareToColumnNameLengthAndUpdate(table, lengthByColumn);
        }

        private static Dictionary<int, int> CompareToColumnNameLengthAndUpdate(DataTable table,
            IReadOnlyDictionary<int, int> lengthByColumnDictionary)
        {
            var dictionary = new Dictionary<int, int>();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var columnNameLength = table.Columns[i].ColumnName.Trim().Length;
                dictionary[i] = columnNameLength > lengthByColumnDictionary[i]
                    ? columnNameLength
                    : lengthByColumnDictionary[i];
            }
            return dictionary;
        }

        private static string PadWithSpaceAndSeparator(string value, int totalColumnLength)
        {
            var remainingSpace = value.Length < totalColumnLength
                ? totalColumnLength - value.Length
                : value.Length - totalColumnLength;
            var spaces = string.Join("", Enumerable.Repeat(" ", remainingSpace).ToArray());
            return value + spaces + " | ";
        }
    }
}