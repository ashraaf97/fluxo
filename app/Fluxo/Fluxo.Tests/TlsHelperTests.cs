using System;
using System.IO;
using System.Net;
using System.Net.Security;
using Fluxo.Core;
using Fluxo.Core.Util;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Guards the TLS hardening: certificates must be validated unless the user
    /// has explicitly opted out. A regression here silently re-enables MITM.
    /// </summary>
    [TestFixture]
    public class TlsHelperTests
    {
        private RemoteCertificateValidationCallback callback;
        private string tempConfigDir;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Isolate config from the real user profile.
            tempConfigDir = Path.Combine(Path.GetTempPath(), $"fluxo-tests-{Guid.NewGuid()}");
            Config.LoadConfig(tempConfigDir);

            TlsHelper.ApplyDefaults();
            callback = ServicePointManager.ServerCertificateValidationCallback;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            try
            {
                if (Directory.Exists(tempConfigDir))
                {
                    Directory.Delete(tempConfigDir, true);
                }
            }
            catch (IOException)
            {
                // Best effort cleanup only.
            }
        }

        [SetUp]
        public void SetUp()
        {
            Config.Instance.AllowInvalidCertificates = false;
        }

        [Test]
        public void ApplyDefaults_InstallsValidationCallback()
        {
            Assert.That(callback, Is.Not.Null);
        }

        [Test]
        public void ApplyDefaults_LeavesProtocolSelectionToTheOs()
        {
            Assert.That(ServicePointManager.SecurityProtocol,
                Is.EqualTo(SecurityProtocolType.SystemDefault));
        }

        [Test]
        public void AllowInvalidCertificates_DefaultsToFalseOnAFreshConfig()
        {
            var freshDir = Path.Combine(Path.GetTempPath(), $"fluxo-tests-{Guid.NewGuid()}");
            try
            {
                Config.LoadConfig(freshDir);
                Assert.That(Config.Instance.AllowInvalidCertificates, Is.False,
                    "Certificate validation must be on unless the user opts out.");
            }
            finally
            {
                Config.LoadConfig(tempConfigDir);
                try
                {
                    if (Directory.Exists(freshDir))
                    {
                        Directory.Delete(freshDir, true);
                    }
                }
                catch (IOException)
                {
                    // Best effort cleanup only.
                }
            }
        }

        [Test]
        public void ValidCertificate_IsAccepted()
        {
            Assert.That(callback(this, null, null, SslPolicyErrors.None), Is.True);
        }

        [TestCase(SslPolicyErrors.RemoteCertificateNameMismatch)]
        [TestCase(SslPolicyErrors.RemoteCertificateChainErrors)]
        [TestCase(SslPolicyErrors.RemoteCertificateNotAvailable)]
        public void InvalidCertificate_IsRejectedByDefault(SslPolicyErrors error)
        {
            Assert.That(callback(this, null, null, error), Is.False,
                $"Certificate error '{error}' must be rejected unless the user opts in.");
        }

        [TestCase(SslPolicyErrors.RemoteCertificateNameMismatch)]
        [TestCase(SslPolicyErrors.RemoteCertificateChainErrors)]
        [TestCase(SslPolicyErrors.RemoteCertificateNotAvailable)]
        public void InvalidCertificate_IsAcceptedOnlyWhenUserOptsIn(SslPolicyErrors error)
        {
            Config.Instance.AllowInvalidCertificates = true;
            Assert.That(callback(this, null, null, error), Is.True);
        }
    }
}
