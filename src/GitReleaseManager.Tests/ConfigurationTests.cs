using System;
using System.IO;
using System.Text;
using GitReleaseManager.Core.Configuration;
using NUnit.Framework;

namespace GitReleaseManager.Tests
{
    [TestFixture]
    public class ConfigurationTests
    {
        [Test]
        public void Should_Read_Label_Aliases()
        {
            // Given
            var text = Resources.Default_Configuration_Yaml;
            using (var stringReader = new StringReader(text))
            {
                // When
                var config = ConfigSerializer.Read(stringReader);

                // Then
                Assert.That(2, Is.EqualTo(config.LabelAliases.Count));
                Assert.That("Bug", Is.EqualTo(config.LabelAliases[0].Name));
                Assert.That("Foo", Is.EqualTo(config.LabelAliases[0].Header));
                Assert.That("Bar", Is.EqualTo(config.LabelAliases[0].Plural));
                Assert.That("Improvement", Is.EqualTo(config.LabelAliases[1].Name));
                Assert.That("Baz", Is.EqualTo(config.LabelAliases[1].Header));
                Assert.That("Qux", Is.EqualTo(config.LabelAliases[1].Plural));
            }
        }

        [Test]
        public void Should_Write_Default_Boolean_Values()
        {
            // Given
            var config = new Config();
            config.Create.IncludeFooter = false; // Just to be sure it is a false value

            // When
            var builder = new StringBuilder();
            using (var writer = new StringWriter(builder))
            {
                ConfigSerializer.Write(config, writer);
            }

            // Then
            Assert.That(builder.ToString(), Contains.Substring("include-footer: false"));
        }

        [Test]
        public void Should_WriteSample_Keys_Without_Values()
        {
            // Given
            var builder = new StringBuilder();

            // When
            using (var writer = new StringWriter(builder))
            {
                ConfigSerializer.WriteSample(writer);
            }

            var text = builder.ToString();

            // Then
            Assert.That(text, Contains.Substring("#create:" + Environment.NewLine));
            Assert.That(text, Contains.Substring("#export:" + Environment.NewLine));
        }

        [Test]
        public void Should_WriteSample_Boolean_Values()
        {
            // Given
            var builder = new StringBuilder();

            // When
            using (var writer = new StringWriter(builder))
            {
                ConfigSerializer.WriteSample(writer);
            }

            var text = builder.ToString();

            // Then
            Assert.That(text, Contains.Substring("#  include-footer: true"));
        }

        [Test]
        public void Should_WriteSample_String_Values()
        {
            // Given
            var builder = new StringBuilder();

            // When
            using (var writer = new StringWriter(builder))
            {
                ConfigSerializer.WriteSample(writer);
            }

            var text = builder.ToString();

            // Then
            Assert.That(text, Contains.Substring("#  sha-section-line-format: '- `{1}\t{0}`'"));
            Assert.That(text, Contains.Substring("#default-branch: master"));
        }

        [Test]
        public void Should_WriteSample_Multiline_String_Values()
        {
            // Given
            var builder = new StringBuilder();

            // When
            using (var writer = new StringWriter(builder))
            {
                ConfigSerializer.WriteSample(writer);
            }

            var text = builder.ToString();

            // Then
            var expectedText = string.Format(
                "#  issue-comment: |-{0}" +
                "#    :tada: This issue has been resolved in version {{milestone}} :tada:{0}" +
                "#{0}" +
                "#    The release is available on:{0}",
                Environment.NewLine);
            Assert.That(text, Contains.Substring(expectedText));
        }
    }
}