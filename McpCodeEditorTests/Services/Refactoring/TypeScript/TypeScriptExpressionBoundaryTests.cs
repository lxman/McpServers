using McpCodeEditor.Services.TypeScript;
using Microsoft.Extensions.Logging;
using Moq;
using McpCodeEditor.Models.TypeScript;
using Xunit.Abstractions;

namespace McpCodeEditorTests.Services.Refactoring.TypeScript
{
    /// <summary>
    /// Unit tests for REF-001: Expression Boundary Detection
    /// Tests the DetectExpressionBoundaries functionality now in ExpressionBoundaryDetectionService
    /// FIXED: Updated for Phase 2 refactoring - now uses ExpressionBoundaryDetectionService directly
    /// </summary>
    public class TypeScriptExpressionBoundaryTests(ITestOutputHelper testOutputHelper)
    {
        private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

        /// <summary>
        /// Creates the ExpressionBoundaryDetectionService for testing
        /// FIXED: No longer uses reflection - directly instantiates the service
        /// </summary>
        private static ExpressionBoundaryDetectionService CreateBoundaryDetectionService()
        {
            var mockLogger = new Mock<ILogger<ExpressionBoundaryDetectionService>>();
            return new ExpressionBoundaryDetectionService(mockLogger.Object);
        }

        #region REF-001: Function Call Boundary Detection Tests

        [Fact]
        public void DetectExpressionBoundaries_MathMax_ShouldDetectFullExpression()
        {
            // Arrange
            const string lineContent = "    return Math.max(10, 20);";
            const int startColumn = 12; // Points to 'M' in Math
            const int endColumn = 27;   // Points to ')' 

            // Expected: Should detect "Math.max(10, 20)" (columns 12-27)
            // Actual (broken): Detects "max(10, 20)" (columns 17-27) - missing "Math."

            // Act & Assert - This test will FAIL until REF-001 is fixed
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            Assert.True(result.Success, $"Boundary detection failed: {result.ErrorMessage}");
            Assert.Equal("Math.max(10, 20)", result.Expression);
            Assert.Equal(12, result.StartColumn);
            Assert.Equal(27, result.EndColumn);
            Assert.Equal("FunctionCall", result.DetectionMethod);
        }

        [Fact]
        public void DetectExpressionBoundaries_InjectFunction_ShouldDetectFullExpression()
        {
            // Arrange
            const string lineContent = "  private readonly accountService: AccountsClient = inject(AccountsClient);";
            const int startColumn = 53; // Points to 'i' in inject
            const int endColumn = 74;   // Points to ')'

            // Expected: Should detect "inject(AccountsClient)" 
            // This is the exact scenario from the MongoDB document

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success, $"Boundary detection failed: {result.ErrorMessage}");
            Assert.Equal("inject(AccountsClient)", result.Expression);
            Assert.Equal(53, result.StartColumn);
            Assert.Equal(74, result.EndColumn);
            Assert.Equal("FunctionCall", result.DetectionMethod);
        }

        [Fact]
        public void DetectExpressionBoundaries_MethodChain_ShouldDetectFullChain()
        {
            // Arrange
            const string lineContent = "    const result = this.service.getData().filter(x => x.active);";
            const int startColumn = 19; // Points to 't' in this
            const int endColumn = 39;   // Points to ')' in getData()

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("this.service.getData()", result.Expression);
            Assert.Equal("FunctionCall", result.DetectionMethod);
        }

        [Fact]
        public void DetectExpressionBoundaries_NestedFunctionCall_ShouldDetectOuterFunction()
        {
            // Arrange
            const string lineContent = "    const result = Math.max(Math.min(10, 20), 5);";
            const int startColumn = 20; // Points to 'M' in Math.max
            const int endColumn = 47;   // Points to last ')'

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Math.max(Math.min(10, 20), 5)", result.Expression);
            Assert.Equal("FunctionCall", result.DetectionMethod);
        }

        #endregion

        #region REF-001: Property Access Boundary Detection Tests

        [Fact]
        public void DetectExpressionBoundaries_PropertyAccess_ShouldDetectFullProperty()
        {
            // Arrange
            const string lineContent = "    console.log(this.accountService.baseUrl);";
            const int startColumn = 17; // Points to 't' in this
            const int endColumn = 42;   // Points to 'l' in baseUrl

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("this.accountService.baseUrl", result.Expression);
            Assert.Equal("PropertyAccess", result.DetectionMethod);
        }

        [Fact]
        public void DetectExpressionBoundaries_ObjectProperty_ShouldDetectSimpleProperty()
        {
            // Arrange
            const string lineContent = "    return user.name;";
            const int startColumn = 12; // Points to 'u' in user
            const int endColumn = 20;   // Points to 'e' in name

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("user.name", result.Expression);
            Assert.Equal("PropertyAccess", result.DetectionMethod);
        }

        #endregion

        #region REF-001: Parentheses Boundary Detection Tests

        [Fact]
        public void DetectExpressionBoundaries_ParenthesesExpression_ShouldDetectBalanced()
        {
            // Arrange
            const string lineContent = "    const result = (a + b) * c;";
            const int startColumn = 20; // Points to '(' 
            const int endColumn = 26;   // Points to ')'

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("(a + b)", result.Expression);
            Assert.Equal("Parentheses", result.DetectionMethod);
        }

        [Fact]
        public void DetectExpressionBoundaries_NestedParentheses_ShouldDetectOutermost()
        {
            // Arrange
            const string lineContent = "    const result = ((a + b) * (c + d));";
            const int startColumn = 20; // Points to first '('
            const int endColumn = 37;   // Points to last ')'

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("((a + b) * (c + d))", result.Expression);
            Assert.Equal("Parentheses", result.DetectionMethod);
        }

        #endregion

        #region REF-001: String Literal Boundary Detection Tests

        [Fact]
        public void DetectExpressionBoundaries_StringLiteral_ShouldDetectWithQuotes()
        {
            // Arrange
            const string lineContent = "    const message = 'Hello, World!';";
            const int startColumn = 21; // Points to first quote
            const int endColumn = 35;   // Points to last quote

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("'Hello, World!'", result.Expression);
            Assert.Equal("StringLiteral", result.DetectionMethod);
        }

        [Fact]
        public void DetectExpressionBoundaries_TemplateLiteral_ShouldDetectWithBackticks()
        {
            // Arrange
            const string lineContent = "    const message = `Hello, ${name}!`;";
            const int startColumn = 21; // Points to backtick
            const int endColumn = 36;   // Points to backtick

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("`Hello, ${name}!`", result.Expression);
            Assert.Equal("StringLiteral", result.DetectionMethod);
        }

        #endregion

        #region REF-001: Edge Cases and Error Conditions

        [Fact]
        public void DetectExpressionBoundaries_EmptySelection_ShouldFail()
        {
            // Arrange
            const string lineContent = "    const x = 123;";
            const int startColumn = 10;
            const int endColumn = 10; // Same as start - empty selection

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("empty", result.ErrorMessage.ToLower());
        }

        [Fact]
        public void DetectExpressionBoundaries_OutOfBounds_ShouldHandleGracefully()
        {
            // Arrange
            const string lineContent = "    const x = 123;";
            const int startColumn = 1;
            const int endColumn = 100; // Beyond line length

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert - Should either succeed with clamped boundaries or fail gracefully
            if (result.Success)
            {
                Assert.True(result.EndColumn <= lineContent.Length);
            }
            else
            {
                Assert.NotNull(result.ErrorMessage);
            }
        }

        [Fact]
        public void DetectExpressionBoundaries_PartialFunctionSelection_ShouldExpandToFullFunction()
        {
            // Arrange - User selects only "max" but should get "Math.max(10, 20)"
            const string lineContent = "    return Math.max(10, 20);";
            const int startColumn = 17; // Points to 'm' in max
            const int endColumn = 19;   // Points to 'x' in max

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert - This is the core REF-001 test case
            Assert.True(result.Success);
            Assert.Equal("Math.max(10, 20)", result.Expression);
            // The boundary detection should expand to include the full qualified call
            Assert.True(result.StartColumn < startColumn, "Should expand leftward to include 'Math.'");
            Assert.True(result.EndColumn > endColumn, "Should expand rightward to include parameters");
        }

        #endregion

        #region REF-001: Boundary Adjustment Logic Tests

        [Fact]
        public void DetectExpressionBoundaries_WhitespaceHandling_ShouldTrimCorrectly()
        {
            // Arrange
            const string lineContent = "    const x =   Math.max(10, 20)   ;";
            const int startColumn = 17; // Points within whitespace before Math
            const int endColumn = 30;   // Points within Math.max

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Math.max(10, 20)", result.Expression);
            Assert.False(result.Expression.StartsWith(" "));
            Assert.False(result.Expression.EndsWith(" "));
        }

        [Fact]
        public void DetectExpressionBoundaries_MultipleStrategies_ShouldPickBestMatch()
        {
            // Arrange - Expression that could match multiple strategies
            const string lineContent = "    const result = user.getName();";
            const int startColumn = 20; // Points to 'u' in user
            const int endColumn = 33;   // Points to ')' 

            // Act
            ExpressionBoundaryResult result = InvokeDetectExpressionBoundaries(lineContent, startColumn, endColumn);

            // Assert - Should prefer FunctionCall over PropertyAccess
            Assert.True(result.Success);
            Assert.Equal("user.getName()", result.Expression);
            Assert.Equal("FunctionCall", result.DetectionMethod);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper method to invoke DetectExpressionBoundaries directly on the service
        /// FIXED: No longer uses reflection - directly calls the service method
        /// </summary>
        private static ExpressionBoundaryResult InvokeDetectExpressionBoundaries(string lineContent, int startColumn, int endColumn)
        {
            ExpressionBoundaryDetectionService service = CreateBoundaryDetectionService();
            return service.DetectExpressionBoundaries(lineContent, startColumn, endColumn);
        }

        #endregion
    }
}
