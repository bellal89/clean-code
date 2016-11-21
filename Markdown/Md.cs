﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markdown.Tokens;

namespace Markdown
{
	public class Md
	{
		private readonly string plainMd;

		private readonly Dictionary<Tag, Func<int, string, int, HtmlToken>> mdTagParserFuncMatch;
		private readonly Dictionary<Tag, Func<int, bool, bool>> validateFunctions;


		public Md(string plainMd)
		{
			this.plainMd = plainMd;
			mdTagParserFuncMatch = new Dictionary<Tag, Func<int, string, int, HtmlToken>>
			{
				[Tag.Em] = ParseEmToken,
				[Tag.Empty] = ParseNoMarkup,
				[Tag.Strong] = ParseStrongToken,
				[Tag.A] = ParseUrl
			};
			validateFunctions = new Dictionary<Tag, Func<int, bool, bool>>
			{
				[Tag.Em] = IsValidEmTag,
				[Tag.Strong] = IsValidStrongTag,
				[Tag.Empty] = (i, b) => false
			};
		}

		private HtmlToken ParseEmToken(int index, string alreadyParsed = "", int alreadyEscaped = 0)
		{
			if (!IsValidEmTag(index, true))
				return ParseNoMarkup(index);

			index++;
			var tokenData = new StringBuilder(alreadyParsed);

			while (index < plainMd.Length && !IsValidEmTag(index, false))
			{
				var tag = ParseTag(index);

				if (tag.Equals(Tag.Strong))
				{
					tokenData.Append("__");
					index += 2;
					continue;
				}

				if (plainMd[index] == '\\')
				{
					index++;
					alreadyEscaped++;
				}

				tokenData.Append(plainMd[index]);
				index++;
			}

			return index != plainMd.Length
				? (HtmlToken) new EmHtmlToken(tokenData.ToString(), alreadyEscaped)
				: new EmptyHtmlToken(tokenData.Insert(0, '_').ToString(), alreadyEscaped);
		}

		private HtmlToken ParseStrongToken(int index, string alreadyParsed = "", int alreadyEscaped = 0)
		{
			if (!IsValidStrongTag(index, true))
				return ParseNoMarkup(index);

			var parsedTokens = new List<HtmlToken>();
			index += 2;
			var tokenData = new StringBuilder(alreadyParsed);

			while (index < plainMd.Length && !IsValidStrongTag(index, false))
			{
				var tag = ParseTag(index);

				if (Equals(tag, Tag.Em))
				{
					parsedTokens.Add(ParseEmInStrong(ref index, ref alreadyEscaped, parsedTokens, tokenData));
					if (index == plainMd.Length)
						break;
				}

				if (plainMd[index] == '\\')
				{
					index++;
					alreadyEscaped++;
				}

				tokenData.Append(plainMd[index]);
				index++;
			}

			parsedTokens.Add(new EmptyHtmlToken(tokenData.ToString(), alreadyEscaped));
			return index != plainMd.Length
				? (HtmlToken) new StrongHtmlToken(parsedTokens, 0)
				: new EmptyHtmlToken(tokenData.Insert(0, "__").ToString(), alreadyEscaped);
		}

		private HtmlToken ParseEmInStrong(ref int index, ref int alreadyEscaped,
			ICollection<HtmlToken> parsedTokens, StringBuilder tokenData)
		{
			parsedTokens.Add(new EmptyHtmlToken(tokenData.ToString(), alreadyEscaped));
			alreadyEscaped = 0;
			tokenData.Clear();
			var htmlToken = ParseEmToken(index);
			index += htmlToken.Length;
			return htmlToken;
		}

		private HtmlToken ParseNoMarkup(int index, string alreadyParsed = "", int alreadyEscaped = 0)
		{
			var tokenData = new StringBuilder(alreadyParsed);
			var escaped = alreadyEscaped;
			while (index < plainMd.Length)
			{
				var tag = ParseTag(index);

				if (validateFunctions[tag].Invoke(index, true))
					break;
				if (plainMd[index] == '\\')
				{
					index++;
					escaped++;
				}
				tokenData.Append(plainMd[index]);
				index++;
			}
			return new EmptyHtmlToken(tokenData.ToString(), escaped);
		}

		private HtmlToken ParseUrl(int index, string alreadyParsed = "", int alreadyEscaped = 0)
		{
			var url = new StringBuilder();
			var returnedValue = ParseInsideBracers(']', index, alreadyEscaped, alreadyParsed);
			var escaped = returnedValue.Escaped;
			index = returnedValue.Index;
			var urlText = returnedValue.Data;

			if (plainMd[index] == '(')
			{
				returnedValue = ParseInsideBracers(')', index, alreadyEscaped, alreadyParsed);
				index = returnedValue.Index;
				return new AHtmlToken((string) urlText, returnedValue.Data, escaped + returnedValue.Escaped);
			}

			throw new MdParserException($"Can't parse link at index {index}");
		}

		private dynamic ParseInsideBracers(char closeBracer, int index, int escaped, string alreadyParsed)
		{
			var data = new StringBuilder(alreadyParsed);
			index++;
			while (index < plainMd.Length && plainMd[index] != closeBracer)
			{
				if (plainMd[index] == '\\')
				{
					index++;
					escaped++;
				}
				data.Append(plainMd[index]);
				index++;
			}
			index++;
			var dataStr = data.ToString();
			return new
			{
				Index = index,
				Escaped = escaped,
				Data = dataStr
			};
		}

		private Tag ParseTag(int tagIndex)
		{
			if (plainMd[tagIndex] == '[')
				return Tag.A;
			if (plainMd[tagIndex] == '_')
			{
				if (tagIndex != plainMd.Length - 1)
					return plainMd[tagIndex + 1] == '_' ? Tag.Strong : Tag.Em;
				return Tag.Em;
			}
			return Tag.Empty;
		}

		private bool NotInsideDigits(int tagIndex)
		{
			if (tagIndex + 1 == plainMd.Length || tagIndex - 1 == -1)
				return true;
			return !char.IsDigit(plainMd[tagIndex - 1]) && !char.IsDigit(plainMd[tagIndex + 1]);
		}

		private bool IsNotStrongTag(int tagIndex)
			=> !(tagIndex - 1 != -1 && plainMd[tagIndex - 1] == '_' ||
			     tagIndex + 1 != plainMd.Length && plainMd[tagIndex + 1] == '_');

		private bool NoSpaceNearMdTag(int tagIndex, int tagLength, bool isOpenTag)
		{
			var nextIndex = tagIndex + (isOpenTag ? tagLength : -1);
			return nextIndex >= 0 && nextIndex < plainMd.Length && plainMd[nextIndex] != ' ';
		}

		private bool IsNotOpenTagInEndOfString(int tagIndex, int tagLength, bool isOpenTag)
			=> !(tagIndex == plainMd.Length - tagLength && isOpenTag);

		private bool IsValidEmTag(int tagIndex, bool isOpenTag)
		{
			if (plainMd[tagIndex] != '_')
				return false;
			return IsNotOpenTagInEndOfString(tagIndex, 1, isOpenTag)
			       && NoSpaceNearMdTag(tagIndex, 1, isOpenTag)
			       && IsNotStrongTag(tagIndex)
			       && NotInsideDigits(tagIndex);
		}

		private bool IsValidStrongTag(int tagIndex, bool isOpenTag)
		{
			if (plainMd[tagIndex] != '_' || !ParseTag(tagIndex).Equals(Tag.Strong))
				return false;
			return IsNotOpenTagInEndOfString(tagIndex, 2, isOpenTag)
			       && NoSpaceNearMdTag(tagIndex, 2, isOpenTag);
		}

		private IEnumerable<HtmlToken> TryParseToHtml()
		{
			var i = 0;
			var root = new List<HtmlToken>();
			while (i < plainMd.Length)
			{
				var tag = ParseTag(i);
				var parsedToken = mdTagParserFuncMatch[tag].Invoke(i, "", 0);
				i += parsedToken.Length;
				root.Add(parsedToken);
			}
			return root;
		}

		public string Render()
		{
			var htmlTokens = TryParseToHtml();
			return string.Join("", htmlTokens.Select(x => x.ToString()));
		}
	}
}