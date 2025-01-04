using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoftShell.Helpers
{
    /// <summary>
    /// Helper methods for formatting text.
    /// </summary>
    public static class TextFormatting
    {
        /// <summary>
        /// Helper method to get a collection of items written out in aligned columns.
        /// </summary>
        /// <typeparam name="T">Type of items.</typeparam>
        /// <param name="items">Items to write, one in each row.</param>
        /// <param name="columnSeparator">Column separator.</param>
        /// <param name="columns">
        /// Definition of columns to write. For each column the following are given:
        /// - Optional header text.
        /// - Delegate to get the object to write in the column for each item.
        /// - Desired alignment of the column.
        /// </param>
        /// <returns>The collection of strings to write out, one per line.</returns>
        public static IEnumerable<string> GetAlignedColumnStrings<T>(IEnumerable<T> items,
                                                              string columnSeparator,
                                                              params (string headerText, Func<T, object> getObject, TextAlignment alignment)[] columns)
        {
            var content = new List<List<(string text, TextAlignment alignment)>>();

            List<(string text, TextAlignment alignment)> columnTexts;

            if (columns.Any(col => !string.IsNullOrWhiteSpace(col.headerText)))
            {
                columnTexts = new List<(string text, TextAlignment alignment)>();
                foreach (var column in columns)
                    columnTexts.Add((text: column.headerText ?? string.Empty, column.alignment));
                content.Add(columnTexts);

                columnTexts = new List<(string text, TextAlignment alignment)>();
                foreach (var column in columns)
                    columnTexts.Add((text: string.Empty.PadRight((column.headerText ?? string.Empty).Length, '-'), column.alignment));

                content.Add(columnTexts);
            }

            foreach (var item in items)
            {
                columnTexts = new List<(string text, TextAlignment alignment)>();

                foreach (var column in columns)
                {
                    columnTexts.Add((text: column.getObject(item)?.ToString() ?? string.Empty, column.alignment));
                }

                content.Add(columnTexts);
            }

            var columnWidths = new List<int>();
            for (var colNo = 0; colNo < columns.Length; colNo++)
                columnWidths.Add(content.Max(row => row[colNo].text.Length));

            var result = new List<string>();

            foreach (var rowCells in content)
            {
                var sb = new StringBuilder();
                for (var colNo = 0; colNo < rowCells.Count; colNo++)
                {
                    var isFirst = colNo == 0;
                    var isLast = colNo == rowCells.Count - 1;

                    if (!isFirst)
                        sb.Append(columnSeparator);

                    switch (rowCells[colNo].alignment)
                    {
                        case TextAlignment.Start:
                            sb.Append(rowCells[colNo].text.PadRight(isLast ? 0 : columnWidths[colNo]));
                            break;
                        case TextAlignment.Center:
                            {
                                var numCharsLeft = (columnWidths[colNo] - rowCells[colNo].text.Length) / 2;
                                sb.Append((string.Empty.PadRight(numCharsLeft) + rowCells[colNo].text).PadRight(isLast ? 0 : columnWidths[colNo]));
                                break;
                            }
                        case TextAlignment.End:
                            sb.Append(rowCells[colNo].text.PadLeft(columnWidths[colNo]));
                            break;
                    }
                }
                result.Add(sb.ToString());
            }

            return result;
        }
    }

    /// <summary>
    /// Text alignment.
    /// </summary>
    public enum TextAlignment
    {
        /// <summary>
        /// Left-aligned.
        /// </summary>
        /// <remarks>The naming is left-to-right or right-to-left language neutral.</remarks>
        Start,

        /// <summary>
        /// Centered.
        /// </summary>
        Center,

        /// <summary>
        /// Right-aligned (for left-to-right culture).
        /// </summary>
        /// <remarks>The naming is left-to-right or right-to-left language neutral.</remarks>
        End
    }
}
