using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Fluxo.Core.Rss
{
    /// <summary>
    /// Reads RSS 2.0 and Atom into articles.
    ///
    /// Elements are matched on local name throughout, ignoring namespaces. Feeds in
    /// the wild declare, omit and misdeclare namespaces freely, and a parser that
    /// insists on them rejects perfectly readable feeds.
    /// </summary>
    public static class RssParser
    {
        /// <summary>
        /// Parses a feed document. Throws <see cref="FormatException"/> when the body
        /// is not XML at all - which is usually an error page rather than a feed.
        /// </summary>
        public static ParsedFeed Parse(string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                throw new FormatException("The feed was empty");
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(xml, LoadOptions.None);
            }
            catch (Exception ex)
            {
                throw new FormatException("The feed could not be read as XML", ex);
            }

            var root = document.Root ?? throw new FormatException("The feed had no root element");

            // RSS nests items under <channel>; Atom hangs entries off the root.
            var channel = Child(root, "channel") ?? root;

            var feed = new ParsedFeed
            {
                Title = Value(Child(channel, "title"))
            };

            foreach (var item in channel.Elements().Where(e => IsItem(e.Name.LocalName)))
            {
                var article = ReadArticle(item);
                if (article != null)
                {
                    feed.Articles.Add(article);
                }
            }

            return feed;
        }

        private static bool IsItem(string localName)
            => localName.Equals("item", StringComparison.OrdinalIgnoreCase)
               || localName.Equals("entry", StringComparison.OrdinalIgnoreCase);

        private static RssArticle? ReadArticle(XElement item)
        {
            var link = ReadLink(item);
            if (string.IsNullOrWhiteSpace(link))
            {
                // Nothing downloadable; a feed of plain news items rather than torrents.
                return null;
            }

            var title = Value(Child(item, "title"));
            var id = Value(Child(item, "guid"))
                     ?? Value(Child(item, "id"))
                     ?? link;

            return new RssArticle
            {
                Id = id!,
                Title = title ?? string.Empty,
                Link = link!,
                Published = ReadDate(item),
                Description = Value(Child(item, "description")) ?? Value(Child(item, "summary"))
            };
        }

        /// <summary>
        /// Where the downloadable URL lives, in order of trust.
        ///
        /// An enclosure is the explicit answer and wins. Otherwise a magnet anywhere
        /// in the link elements beats an ordinary URL, because a feed that offers both
        /// usually points its plain link at a description page rather than the file.
        /// </summary>
        private static string? ReadLink(XElement item)
        {
            var enclosure = item.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals("enclosure", StringComparison.OrdinalIgnoreCase));
            var enclosureUrl = Attribute(enclosure, "url");
            if (!string.IsNullOrWhiteSpace(enclosureUrl))
            {
                return enclosureUrl;
            }

            var links = item.Elements()
                .Where(e => e.Name.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase))
                .Select(e => string.IsNullOrWhiteSpace(e.Value) ? Attribute(e, "href") : e.Value.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            return links.FirstOrDefault(IsMagnet) ?? links.FirstOrDefault();
        }

        private static bool IsMagnet(string? value)
            => value != null && value.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// RSS dates are RFC 822 and Atom's are ISO 8601, and plenty of feeds get
        /// either subtly wrong. An unreadable date returns null rather than a
        /// misleading one, and callers treat that as "unknown".
        /// </summary>
        internal static DateTime? ReadDate(XElement item)
        {
            var raw = Value(Child(item, "pubDate"))
                      ?? Value(Child(item, "published"))
                      ?? Value(Child(item, "updated"))
                      ?? Value(Child(item, "date"));

            return ParseDate(raw);
        }

        internal static DateTime? ParseDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var text = raw!.Trim();

            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out var offset))
            {
                return offset.UtcDateTime;
            }

            // RFC 822 with an alphabetic zone such as "GMT", which TryParse rejects
            // on some cultures, plus the two-digit-year form still seen in the wild.
            string[] formats =
            {
                "ddd, dd MMM yyyy HH:mm:ss zzz",
                "ddd, dd MMM yyyy HH:mm:ss",
                "dd MMM yyyy HH:mm:ss zzz",
                "dd MMM yyyy HH:mm:ss",
                "ddd, dd MMM yy HH:mm:ss zzz"
            };

            var trimmed = StripAlphabeticZone(text);
            if (DateTime.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// Drops a trailing "GMT"/"UTC" so the numeric formats above can be applied.
        /// Only these two are treated as UTC; a named zone like "EST" is ambiguous
        /// and is better left unparsed than guessed at.
        /// </summary>
        private static string StripAlphabeticZone(string text)
        {
            foreach (var zone in new[] { " GMT", " UTC", " UT", " Z" })
            {
                if (text.EndsWith(zone, StringComparison.OrdinalIgnoreCase))
                {
                    return text.Substring(0, text.Length - zone.Length).Trim();
                }
            }
            return text;
        }

        private static XElement? Child(XElement parent, string localName)
            => parent.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

        private static string? Value(XElement? element)
        {
            var text = element?.Value?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static string? Attribute(XElement? element, string name)
        {
            var value = element?.Attributes()
                .FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }

    /// <summary>What a single fetch of a feed yielded.</summary>
    public class ParsedFeed
    {
        /// <summary>The feed's own title, used to name a newly added subscription.</summary>
        public string? Title { get; set; }

        public IList<RssArticle> Articles { get; } = new List<RssArticle>();
    }
}
