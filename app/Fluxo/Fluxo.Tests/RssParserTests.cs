using System;
using System.Linq;
using Fluxo.Core.Rss;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Feeds in the wild are inconsistent: RSS or Atom, namespaced or not, with the
    /// torrent URL in an enclosure, a link, or both. The parser has to survive all of
    /// it, and a feed that silently yields no articles looks identical to one with
    /// nothing new in it - so these are worth pinning down.
    /// </summary>
    [TestFixture]
    public class RssParserTests
    {
        // ------------------------------------------------------------------ RSS

        [Test]
        public void Parse_ReadsARssItem()
        {
            var feed = RssParser.Parse(@"<?xml version='1.0'?>
                <rss version='2.0'><channel>
                  <title>Example feed</title>
                  <item>
                    <title>Some.Release.S01E01</title>
                    <link>magnet:?xt=urn:btih:abc</link>
                    <guid>item-1</guid>
                    <pubDate>Tue, 12 Aug 2025 10:30:00 GMT</pubDate>
                  </item>
                </channel></rss>");

            Assert.That(feed.Title, Is.EqualTo("Example feed"));
            Assert.That(feed.Articles.Count, Is.EqualTo(1));
            Assert.That(feed.Articles[0].Title, Is.EqualTo("Some.Release.S01E01"));
            Assert.That(feed.Articles[0].Link, Is.EqualTo("magnet:?xt=urn:btih:abc"));
            Assert.That(feed.Articles[0].Id, Is.EqualTo("item-1"));
            Assert.That(feed.Articles[0].Published, Is.Not.Null);
        }

        [Test]
        public void Parse_PrefersTheEnclosureOverTheLink()
        {
            // The plain link on such feeds points at a description page, not the file.
            var feed = RssParser.Parse(@"<rss><channel><item>
                  <title>Release</title>
                  <link>https://example.com/details/1</link>
                  <enclosure url='https://example.com/f/1.torrent' type='application/x-bittorrent'/>
                </item></channel></rss>");

            Assert.That(feed.Articles[0].Link, Is.EqualTo("https://example.com/f/1.torrent"));
        }

        [Test]
        public void Parse_PrefersAMagnetOverAnOrdinaryLink()
        {
            var feed = RssParser.Parse(@"<rss><channel><item>
                  <title>Release</title>
                  <link>https://example.com/details/1</link>
                  <link>magnet:?xt=urn:btih:abc</link>
                </item></channel></rss>");

            Assert.That(feed.Articles[0].Link, Is.EqualTo("magnet:?xt=urn:btih:abc"));
        }

        [Test]
        public void Parse_SkipsItemsWithNothingToDownload()
        {
            var feed = RssParser.Parse(@"<rss><channel>
                  <item><title>Just news</title><description>no link</description></item>
                  <item><title>Real</title><link>magnet:?xt=urn:btih:abc</link></item>
                </channel></rss>");

            Assert.That(feed.Articles.Count, Is.EqualTo(1));
            Assert.That(feed.Articles[0].Title, Is.EqualTo("Real"));
        }

        [Test]
        public void Parse_FallsBackToTheLinkAsTheId()
        {
            // Without a stable id the link is what stops a re-read downloading twice.
            var feed = RssParser.Parse(@"<rss><channel><item>
                  <title>Release</title><link>magnet:?xt=urn:btih:abc</link>
                </item></channel></rss>");

            Assert.That(feed.Articles[0].Id, Is.EqualTo("magnet:?xt=urn:btih:abc"));
        }

        // ----------------------------------------------------------------- Atom

        [Test]
        public void Parse_ReadsAnAtomEntry()
        {
            var feed = RssParser.Parse(@"<?xml version='1.0'?>
                <feed xmlns='http://www.w3.org/2005/Atom'>
                  <title>Atom feed</title>
                  <entry>
                    <title>Another.Release.S02E03</title>
                    <link href='magnet:?xt=urn:btih:def'/>
                    <id>entry-9</id>
                    <updated>2025-08-12T10:30:00Z</updated>
                  </entry>
                </feed>");

            Assert.That(feed.Title, Is.EqualTo("Atom feed"));
            Assert.That(feed.Articles.Count, Is.EqualTo(1));
            Assert.That(feed.Articles[0].Link, Is.EqualTo("magnet:?xt=urn:btih:def"));
            Assert.That(feed.Articles[0].Id, Is.EqualTo("entry-9"));
        }

        [Test]
        public void Parse_IgnoresNamespacePrefixes()
        {
            // Declared prefixes must not change which elements are recognised.
            var feed = RssParser.Parse(@"<x:rss xmlns:x='http://example.com/ns'><x:channel>
                  <x:item><x:title>Prefixed</x:title><x:link>magnet:?xt=urn:btih:abc</x:link></x:item>
                </x:channel></x:rss>");

            Assert.That(feed.Articles.Count, Is.EqualTo(1));
            Assert.That(feed.Articles[0].Title, Is.EqualTo("Prefixed"));
        }

        // --------------------------------------------------------------- errors

        [Test]
        public void Parse_ThrowsWhenTheBodyIsNotXml()
        {
            // Usually an error page or a captive portal rather than a feed.
            Assert.Throws<FormatException>(() => RssParser.Parse("<html><body>404</body></html></p>"));
            Assert.Throws<FormatException>(() => RssParser.Parse(string.Empty));
            Assert.Throws<FormatException>(() => RssParser.Parse(null));
        }

        [Test]
        public void Parse_ReturnsNoArticlesForAnEmptyFeed()
        {
            var feed = RssParser.Parse("<rss><channel><title>Quiet</title></channel></rss>");
            Assert.That(feed.Articles.Count, Is.EqualTo(0));
        }

        // ----------------------------------------------------------------- dates

        [Test]
        public void ParseDate_ReadsRfc822AndIso8601()
        {
            Assert.That(RssParser.ParseDate("Tue, 12 Aug 2025 10:30:00 GMT"), Is.Not.Null);
            Assert.That(RssParser.ParseDate("2025-08-12T10:30:00Z"), Is.Not.Null);
            Assert.That(RssParser.ParseDate("Tue, 12 Aug 2025 10:30:00 +0200"), Is.Not.Null);
        }

        [Test]
        public void ParseDate_IsNullWhenUnreadable()
        {
            // Null means "unknown", which callers treat differently from the epoch.
            Assert.That(RssParser.ParseDate("last thursday"), Is.Null);
            Assert.That(RssParser.ParseDate(string.Empty), Is.Null);
            Assert.That(RssParser.ParseDate(null), Is.Null);
        }
    }
}
