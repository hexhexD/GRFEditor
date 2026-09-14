using System;
using System.Collections.Generic;
using Utilities.Extension;

namespace GRF.FileFormats.LubFormat {
	/// <summary>
	/// Helping class for the CodeAnalyser.
	/// This class checks for various properties on lines.
	/// </summary>
	public static class LineHelper {
		/// <summary>
		/// Determines whether the specified line is an if line.
		/// </summary>
		/// <param name="line">The line.</param>
		/// <returns>
		///   <c>true</c> if the specified line is if; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsIf(string line) {
			return IsStart(line, "if ");
		}

		public static bool IsControl(string line) {
			var l = NoIndent(line);
			return l.StartsWith("if ", StringComparison.Ordinal) || l.StartsWith("for ", StringComparison.Ordinal) || l.StartsWith("while ", StringComparison.Ordinal);
		}

		/// <summary>
		/// Removes the indent on the line.
		/// </summary>
		/// <param name="line">The line.</param>
		/// <returns>A line without indent</returns>
		public static string NoIndent(string line) {
			return line.TrimStart('\t');
		}

		/// <summary>
		/// Removes everything after the indent and puts the new value.
		/// </summary>
		/// <param name="line">The line.</param>
		/// <param name="newValue">The new value.</param>
		/// <returns></returns>
		public static string ReplaceAfterIndent(string line, string newValue) {
			int numOfTabs = line.Length - NoIndent(line).Length;
			return line.Remove(numOfTabs) + newValue;
		}

		/// <summary>
		/// Gets the indent of a line.
		/// </summary>
		/// <param name="line">The line.</param>
		/// <returns>The line indent.</returns>
		public static int GetIndent(string line) {
			int indent = 0;

			for (int j = 0; j < line.Length; j++) {
				if (line[j] == '\t')
					indent++;
				else
					break;
			}

			return indent;
		}

		public static int GetLineIndexContains(List<string> lines, string toFind, int startIndex) {
			for (int i = startIndex; i < lines.Count; i++) {
				if (lines[i].IndexOf(" function(", StringComparison.Ordinal) > -1) {
					int endNesting = 1;
					i++;

					for (; i < lines.Count; i++) {
						var line = lines[i];
						int lineStartIndex = 0;

						while (lineStartIndex < line.Length && line[lineStartIndex] == '\t') lineStartIndex++;

						if (lineStartIndex == 0 && line.Equals("end", StringComparison.Ordinal))
							endNesting--;
						else if (lineStartIndex > 0) {
							if (StartsWithOrdinal(line, lineStartIndex, "end"))
								endNesting--;
							else if (
								StartsWithOrdinal(line, lineStartIndex, "end") ||
								StartsWithOrdinal(line, lineStartIndex, "if") ||
								StartsWithOrdinal(line, lineStartIndex, "while"))
								endNesting++;
						}

						if (endNesting <= 0) {
							i++;

							if (i >= lines.Count)
								return -1;

							break;
						}
					}
				}

				for (int j = 0, k = 0; j < lines[i].Length && k < toFind.Length; j++) {
					if (lines[i][j] == '\t')
						continue;
					if (lines[i][j] == toFind[k++]) {
						if (k == toFind.Length)
							return i;
						continue;
					}
					break;
				}
			}

			return -1;
		}

		public static bool StartsWithOrdinal(string source, int offset, string value) {
			if (source.Length - offset < value.Length) return false;
			for (int i = 0; i < value.Length; i++) {
				if (source[offset + i] != value[i]) return false;
			}
			return true;
		}

		public static int GetLineIndexEndsWith(List<string> lines, string toFind, int startIndex) {
			for (int i = startIndex; i < lines.Count; i++) {
				if (lines[i].EndsWith(toFind, StringComparison.Ordinal))
					return i;
			}

			return -1;
		}

		/// <summary>
		/// Determines whether the specified line is empty.
		/// </summary>
		/// <param name="line">The line.</param>
		/// <returns>
		///   <c>true</c> if the specified line is empty; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsEmpty(string line) {
			return line == "" || line.EndsWith("\t", StringComparison.Ordinal);
		}

		/// <summary>
		/// Determines whether the specified line starts with the specified value (ignores indent).
		/// </summary>
		/// <param name="line">The line.</param>
		/// <param name="value">The value to find.</param>
		/// <returns>
		///   <c>true</c> if the specified line starts with the value; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsStart(string line, string value) {
			int i = 0;

			while (i < line.Length && line[i] == '\t')
				i++;

			if (line.Length - i < value.Length) return false;
			for (int j = 0; j < value.Length; j++) {
				if (value[j] != line[j + i])
					return false;
			}

			return true;
		}

		/// <summary>
		/// Swaps the specified lines.
		/// </summary>
		/// <param name="lines">The lines.</param>
		/// <param name="from">From.</param>
		/// <param name="to">To.</param>
		public static void Swap(List<string> lines, int from, int to) {
			string old = lines[@from];
			lines[@from] = lines[to];
			lines[to] = old;
		}

		public static List<string> FixIndent(List<string> replacedLines, int codeIndent) {
			int gotoIndent = GetIndent(replacedLines[0]);
			string toReplaceFrom = "";
			string toReplaceTo = "";

			int toAdd = codeIndent - gotoIndent;

			if (toAdd != 0) {
				if (toAdd > 0) {
					for (int i = 0; i < toAdd; i++)
						toReplaceTo += "\t";
				}
				else {
					toAdd = -1 * toAdd;

					for (int i = 0; i < toAdd; i++)
						toReplaceFrom += "\t";
				}

				for (int i = 0; i < replacedLines.Count; i++) {
					replacedLines[i] = replacedLines[i].ReplaceOnce(toReplaceFrom, toReplaceTo);
				}
			}

			return replacedLines;
		}

		public static string GenerateIndent(int indent) {
			return ExtensionMethods.GetIndentString(indent);
		}

		public static string GetLabelFromGoto(string line) {
			return NoIndent(line).Replace("goto ", "");
		}
	}
}