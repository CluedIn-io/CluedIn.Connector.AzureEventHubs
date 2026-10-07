using System.Text.RegularExpressions;
using CluedIn.Connector.AzureEventHub;
using Xunit;

namespace CluedIn.Connector.AzureEventHub.Unit.Tests
{
    public class AzureEventHubConstantsTests
    {
        // -------------------------------------------------------------------------
        // BuildBatchSizeValidationRegex
        // The regex is used by the UI to flag INVALID values, so it must:
        //   • NOT match values inside [min, max]  (valid input ? no error)
        //   •     MATCH values outside [min, max] (invalid input ? show error)
        // -------------------------------------------------------------------------

        public class BuildBatchSizeValidationRegexTests
        {
            private static bool IsInvalid(string pattern, string value)
                => Regex.IsMatch(value, pattern);

            // -- valid values: lower bound, upper bound, middle --

            [Theory]
            [InlineData(1)]
            [InlineData(25)]
            [InlineData(50)]
            public void ValidValue_DoesNotMatch(int value)
            {
                var pattern = AzureEventHubConstants.BuildBatchSizeValidationRegex(1, 50);

                Assert.False(IsInvalid(pattern, value.ToString()),
                    $"Pattern should not flag valid value {value}");
            }

            // -- invalid values: zero, below min, above max, negative --

            [Theory]
            [InlineData("0")]
            [InlineData("51")]
            [InlineData("100")]
            [InlineData("-1")]
            public void OutOfRangeInteger_Matches(string value)
            {
                var pattern = AzureEventHubConstants.BuildBatchSizeValidationRegex(1, 50);

                Assert.True(IsInvalid(pattern, value),
                    $"Pattern should flag out-of-range value '{value}'");
            }

            // -- invalid values: non-numeric input --

            [Theory]
            [InlineData("")]
            [InlineData(" ")]
            [InlineData("abc")]
            [InlineData("1.5")]
            [InlineData("2 0")]
            public void NonNumericInput_Matches(string value)
            {
                var pattern = AzureEventHubConstants.BuildBatchSizeValidationRegex(1, 50);

                Assert.True(IsInvalid(pattern, value),
                    $"Pattern should flag non-numeric value '{value}'");
            }

            // -- every integer in [1, 50] is individually valid --

            [Fact]
            public void AllValuesInRange_NoneMatch()
            {
                var pattern = AzureEventHubConstants.BuildBatchSizeValidationRegex(1, 50);

                for (int i = 1; i <= 50; i++)
                    Assert.False(IsInvalid(pattern, i.ToString()),
                        $"Pattern should not flag in-range value {i}");
            }

            // -- custom range: works correctly for ranges other than 1-50 --

            [Theory]
            [InlineData(5, 10, "5",  false)]
            [InlineData(5, 10, "10", false)]
            [InlineData(5, 10, "7",  false)]
            [InlineData(5, 10, "4",  true)]
            [InlineData(5, 10, "11", true)]
            [InlineData(5, 10, "0",  true)]
            public void CustomRange_BoundaryBehaviour(int min, int max, string value, bool expectInvalid)
            {
                var pattern = AzureEventHubConstants.BuildBatchSizeValidationRegex(min, max);

                Assert.Equal(expectInvalid, IsInvalid(pattern, value));
            }

            // -- the production constants produce a coherent regex --

            [Fact]
            public void ProductionConstants_RegexIsConsistent()
            {
                var pattern = AzureEventHubConstants.BuildBatchSizeValidationRegex(
                    AzureEventHubConstants.MinBatchSize,
                    AzureEventHubConstants.DefaultFlushSize);

                // Min is valid
                Assert.False(IsInvalid(pattern, AzureEventHubConstants.MinBatchSize.ToString()));
                // Max is valid
                Assert.False(IsInvalid(pattern, AzureEventHubConstants.DefaultFlushSize.ToString()));
                // One below min is invalid
                Assert.True(IsInvalid(pattern, (AzureEventHubConstants.MinBatchSize - 1).ToString()));
                // One above max is invalid
                Assert.True(IsInvalid(pattern, (AzureEventHubConstants.DefaultFlushSize + 1).ToString()));
            }
        }
    }
}
