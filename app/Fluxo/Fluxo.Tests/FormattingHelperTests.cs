using System.Globalization;
using System.Threading;
using Fluxo.Core.Util;
using NUnit.Framework;

namespace Fluxo.Tests
{
    [TestFixture]
    public class FormattingHelperTests
    {
        private CultureInfo originalCulture;

        [SetUp]
        public void SetUp()
        {
            // FormatSize uses "F1", which is culture sensitive. Pin the culture so
            // the expectations below hold on any build agent.
            originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        [TearDown]
        public void TearDown()
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [TestCase(0, "00:00:00")]
        [TestCase(9, "00:00:09")]
        [TestCase(59, "00:00:59")]
        [TestCase(60, "00:01:00")]
        [TestCase(3661, "01:01:01")]
        [TestCase(86399, "23:59:59")]
        public void ToHMS_FormatsAsZeroPaddedHoursMinutesSeconds(long seconds, string expected)
        {
            Assert.That(FormattingHelper.ToHMS(seconds), Is.EqualTo(expected));
        }

        [TestCase(0, "---")]
        [TestCase(-1, "---")]
        public void FormatSize_ReportsUnknownForNonPositiveLengths(double length, string expected)
        {
            Assert.That(FormattingHelper.FormatSize(length), Is.EqualTo(expected));
        }

        [TestCase(1, "1B")]
        [TestCase(512, "512B")]
        // Boundaries are exclusive: exactly 1 KiB still renders in bytes.
        [TestCase(1024, "1024B")]
        [TestCase(1536, "1.5K")]
        [TestCase(2097152, "2.0M")]
        [TestCase(3221225472, "3.0G")]
        public void FormatSize_ScalesToBinaryUnits(double length, string expected)
        {
            Assert.That(FormattingHelper.FormatSize(length), Is.EqualTo(expected));
        }
    }
}
