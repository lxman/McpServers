using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Options;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Models.Validation;
using McpCodeEditor.Services.Refactoring.CSharp;
using McpCodeEditor.Services.Validation;
using Microsoft.Extensions.Logging;
using Moq;

namespace McpCodeEditorTests.Services.Refactoring.CSharp
{
    /// <summary>
    /// Integration tests for the C# method extraction functionality.
    /// Tests the actual CSharpMethodExtractor implementation.
    /// </summary>
    public class CSharpMethodExtractionTests
    {
        private readonly CSharpMethodExtractor _extractor;
        private readonly Mock<ExtractMethodValidator> _mockValidator;

        public CSharpMethodExtractionTests()
        {
            var mockPathValidationService = new Mock<IPathValidationService>();
            var mockBackupService = new Mock<IBackupService>();
            _mockValidator = new Mock<ExtractMethodValidator>();
            var mockMethodCallGenerationService = new Mock<IMethodCallGenerationService>();
            var mockMethodSignatureGenerationService = new Mock<IMethodSignatureGenerationService>();
            var mockCodeModificationService = new Mock<ICodeModificationService>();
            var mockEnhancedVariableAnalysisService = new Mock<IEnhancedVariableAnalysisService>();
            var mockChangeTrackingService = new Mock<IChangeTrackingService>();
            var mockLogger = new Mock<ILogger<CSharpMethodExtractor>>();

            _extractor = new CSharpMethodExtractor(
                mockPathValidationService.Object,
                mockBackupService.Object,
                mockLogger.Object,
                mockMethodCallGenerationService.Object,
                mockMethodSignatureGenerationService.Object,
                mockCodeModificationService.Object,
                mockEnhancedVariableAnalysisService.Object,
                mockChangeTrackingService.Object
            );

            // Setup default path validation behavior
            mockPathValidationService.Setup(p => p.ValidateAndResolvePath(It.IsAny<string>()))
                .Returns<string>(path => path);
        }

        #region Basic Extraction Tests

        [Fact]
        public async Task ExtractMethod_SimpleCodeBlock_Success()
        {
            // Arrange
            var context = new RefactoringContext
            {
                FilePath = "test.cs",
                Language = LanguageType.CSharp,
                FileContent = @"
using System;

namespace Test
{
    public class Calculator
    {
        public void Calculate()
        {
            int x = 10;
            int y = 20;
            
            // Extract from here
            int result = x + y;
            Console.WriteLine($""Result: {result}"");
            // Extract to here
            
            Console.WriteLine(""Done"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateSum",
                StartLine = 13,
                EndLine = 15,
                AccessModifier = "private",
                IsStatic = false
            };

            // Create extracted method info
            var extractedMethodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private int CalculateSum(int x, int y)",
                MethodBody = "int result = x + y;\nConsole.WriteLine($\"Result: {result}\");\nreturn result;",
                MethodCall = "int result = CalculateSum(x, y);",
                StartLine = 13,
                EndLine = 15,
                ReturnType = "int",
                AccessModifier = "private"
            };

            // Setup validator to return success using factory method
            MethodExtractionValidationResult validationResult = MethodExtractionValidationResult.Success(extractedMethodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "Calculate",
                CyclomaticComplexity = 1,
                HasReturnStatements = false,
                RequiresReturnValue = true,
                SuggestedReturnType = "int",
                SuggestedParameters = ["int x", "int y"]
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            RefactoringResult result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("CalculateSum", result.Message);
            Assert.NotNull(result.Changes);
            Assert.Single(result.Changes);
            
            FileChange change = result.Changes.First();
            Assert.Contains("private int CalculateSum(int x, int y)", change.ModifiedContent);
            Assert.Contains("return result;", change.ModifiedContent);
        }

        [Fact]
        public async Task ExtractMethod_InvalidLineRange_ReturnsError()
        {
            // Arrange
            var context = new RefactoringContext
            {
                FilePath = "test.cs",
                Language = LanguageType.CSharp,
                FileContent = "public class Test { }"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "ExtractedMethod",
                StartLine = 100,
                EndLine = 105,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup validator to return error using factory method
            var errors = new List<ValidationError>
            {
                new ValidationError("INVALID_RANGE", "End line (105) exceeds file length")
            };
            var validationResult = MethodExtractionValidationResult.Failure<MethodExtractionValidationResult>(errors, "Validation failed");

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            RefactoringResult result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Validation failed", result.Error);
        }

        #endregion

        #region Return Type Detection Tests

        [Fact]
        public async Task ExtractMethod_SingleVariableModified_ReturnsValue()
        {
            // Arrange
            var context = new RefactoringContext
            {
                FilePath = "test.cs",
                Language = LanguageType.CSharp,
                FileContent = @"
public class Test
{
    public void Process()
    {
        int x = 10;
        int y = 20;
        
        // Extract this
        int sum = x + y;
        
        Console.WriteLine($""Sum: {sum}"");
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateSum",
                StartLine = 9,
                EndLine = 10,
                AccessModifier = "private",
                IsStatic = false
            };

            // Create extracted method info
            var extractedMethodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private int CalculateSum(int x, int y)",
                MethodBody = "int sum = x + y;\nreturn sum;",
                MethodCall = "int sum = CalculateSum(x, y);",
                StartLine = 9,
                EndLine = 10,
                ReturnType = "int",
                AccessModifier = "private"
            };

            // Setup validator for single return value
            MethodExtractionValidationResult validationResult = MethodExtractionValidationResult.Success(extractedMethodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "Process",
                RequiresReturnValue = true,
                SuggestedReturnType = "int",
                SuggestedParameters = ["int x", "int y"],
                LocalVariables = ["sum"],  // sum is declared IN the extraction
                ModifiedVariables = ["sum"]
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            RefactoringResult result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success);
            FileChange change = result.Changes.First();
            Assert.Contains("private int CalculateSum(int x, int y)", change.ModifiedContent);
            Assert.Contains("return sum;", change.ModifiedContent);
            // Should create proper variable declaration in the call
            Assert.Contains("int sum = CalculateSum(x, y);", change.ModifiedContent);
        }

        #endregion

        #region Bug Regression Tests

        [Fact]
        public async Task BugTest_VariableDeclaredInExtraction_AddsTypeInCall()
        {
            // This test specifically addresses the bug where type declaration
            // is missing when a variable is declared in the extraction
            
            var context = new RefactoringContext
            {
                FilePath = "SimpleExtractionTest.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace SimpleTest
{
    public class Calculator
    {
        public void Calculate()
        {
            int x = 10;
            int y = 20;
            
            // Simple calculation to extract
            int result = x + y;
            result = result * 2;
            Console.WriteLine($""Result: {result}"");
            
            // Use result after extraction
            int finalValue = result + 100;
            Console.WriteLine($""Final: {finalValue}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateDoubledSum",
                StartLine = 13,
                EndLine = 15,
                AccessModifier = "private",
                IsStatic = false
            };

            // Create extracted method info
            var extractedMethodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private int CalculateDoubledSum(int x, int y)",
                MethodBody = "int result = x + y;\nresult = result * 2;\nConsole.WriteLine($\"Result: {result}\");\nreturn result;",
                MethodCall = "int result = CalculateDoubledSum(x, y);",
                StartLine = 13,
                EndLine = 15,
                ReturnType = "int",
                AccessModifier = "private"
            };

            // Setup proper validation result
            MethodExtractionValidationResult validationResult = MethodExtractionValidationResult.Success(extractedMethodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "Calculate",
                RequiresReturnValue = true,
                SuggestedReturnType = "int",
                SuggestedParameters = ["int x", "int y"],
                LocalVariables = ["result"],  // result is declared IN the extraction
                ModifiedVariables = ["result"]
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            RefactoringResult result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success);
            FileChange change = result.Changes.First();
            
            // The critical assertion - should include type declaration
            // because 'result' is declared within the extracted code
            Assert.Contains("int result = CalculateDoubledSum(x, y);", change.ModifiedContent);
            
            // Check that it's not just an assignment without type (need to be more specific)
            // Split by lines and check that no line starts with just "result = " (without leading type)
            string[] lines = change.ModifiedContent.Split('\n');
            Assert.DoesNotContain(lines, line => line.Trim() == "result = CalculateDoubledSum(x, y);");
        }

        [Fact]
        public async Task BugTest_MultipleVariablesModified_ProperHandling()
        {
            // Test for the bug where multiple variable modifications aren't detected
            
            var context = new RefactoringContext
            {
                FilePath = "test.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;
using System.Collections.Generic;

namespace Test
{
    public class DataProcessor
    {
        private List<int> data = new List<int>();
        
        public void ProcessData()
        {
            // Initialize data
            for (int i = 0; i < 100; i++)
            {
                data.Add(i * 2);
            }
            
            // Calculate statistics - THIS SHOULD BE EXTRACTED
            int sum = 0;
            int max = int.MinValue;
            int min = int.MaxValue;
            
            foreach (var value in data)
            {
                sum += value;
                if (value > max) max = value;
                if (value < min) min = value;
            }
            
            double average = sum / (double)data.Count;
            
            // Display results
            Console.WriteLine($""Sum: {sum}"");
            Console.WriteLine($""Average: {average}"");
            Console.WriteLine($""Max: {max}"");
            Console.WriteLine($""Min: {min}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateStatistics",
                StartLine = 19,
                EndLine = 30,
                AccessModifier = "private",
                IsStatic = false
            };

            // Create extracted method info
            var extractedMethodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private void CalculateStatistics()",
                MethodBody = "int sum = 0;\nint max = int.MinValue;\nint min = int.MaxValue;\n\nforeach (var value in data)\n{\n    sum += value;\n    if (value > max) max = value;\n    if (value < min) min = value;\n}\n\ndouble average = sum / (double)data.Count;",
                MethodCall = "CalculateStatistics();",
                StartLine = 19,
                EndLine = 30,
                ReturnType = "void",
                AccessModifier = "private"
            };

            // Setup validation with multiple modified variables
            MethodExtractionValidationResult validationResult = MethodExtractionValidationResult.Success(extractedMethodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "ProcessData",
                RequiresReturnValue = false, // Too many variables to return
                SuggestedReturnType = "void",
                SuggestedParameters = [],
                ModifiedVariables = ["sum", "max", "min", "average"],
                LocalVariables = ["sum", "max", "min", "average"]
            };
            validationResult.AddWarning("MULTIPLE_VARIABLES", "Code modifies 4 variables. Consider using ref parameters or smaller extraction.");

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            RefactoringResult result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("warning", result.Message.ToLower());
            
            // Check metadata
            Dictionary<string, object> metadata = result.Metadata;
            Assert.NotNull(metadata);
            Assert.Contains("validationWarnings", metadata.Keys);
            Assert.Contains("variable", metadata["validationWarnings"].ToString()!.ToLower());
        }

        #endregion
    }
}
