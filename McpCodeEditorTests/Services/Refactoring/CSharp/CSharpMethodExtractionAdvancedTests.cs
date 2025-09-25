using McpCodeEditor.Interfaces;
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
    /// Advanced integration tests for the C# method extraction functionality.
    /// Tests complex scenarios including multiple variable modifications, ref parameters,
    /// tuple returns, complex scopes, and edge cases that currently fail.
    /// </summary>
    public class CSharpMethodExtractionAdvancedTests
    {
        private readonly CSharpMethodExtractor _extractor;
        private readonly Mock<ExtractMethodValidator> _mockValidator;

        public CSharpMethodExtractionAdvancedTests()
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

            // Setup the default path validation behavior
            mockPathValidationService.Setup(p => p.ValidateAndResolvePath(It.IsAny<string>()))
                .Returns<string>(path => path);
        }

        #region Multiple Variable Modifications Tests

        [Fact]
        public async Task ExtractMethod_MultipleVariableAccumulators_ShouldUseTupleReturn()
        {
            // Test scenario: Extracting code that modifies multiple accumulator variables
            // This currently fails with validation errors
            
            var context = new RefactoringContext
            {
                FilePath = "OrderProcessor.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;
using System.Collections.Generic;
using System.Linq;

namespace Accounting
{
    public class OrderProcessor
    {
        private List<Order> orders = new List<Order>();
        
        public void ProcessOrders()
        {
            // Initialize test data
            orders.Add(new Order { Id = 1, Amount = 100.50m, Status = ""Pending"" });
            orders.Add(new Order { Id = 2, Amount = 250.75m, Status = ""Completed"" });
            orders.Add(new Order { Id = 3, Amount = 50.25m, Status = ""Pending"" });
            
            // Complex calculation block to extract - modifies 4 variables
            decimal totalPending = 0;
            decimal totalCompleted = 0;
            int pendingCount = 0;
            int completedCount = 0;
            
            foreach (var order in orders)
            {
                if (order.Status == ""Pending"")
                {
                    totalPending += order.Amount;
                    pendingCount++;
                }
                else if (order.Status == ""Completed"")
                {
                    totalCompleted += order.Amount;
                    completedCount++;
                }
            }
            
            // Use the calculated values
            decimal averagePending = pendingCount > 0 ? totalPending / pendingCount : 0;
            decimal averageCompleted = completedCount > 0 ? totalCompleted / completedCount : 0;
            
            Console.WriteLine($""Pending: {pendingCount} orders, ${totalPending:F2} total"");
            Console.WriteLine($""Completed: {completedCount} orders, ${totalCompleted:F2} total"");
            Console.WriteLine($""Averages - Pending: ${averagePending:F2}, Completed: ${averageCompleted:F2}"");
        }
    }
    
    public class Order
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateOrderStatistics",
                StartLine = 19,
                EndLine = 35,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup validator for tuple return scenario
            var methodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private (decimal, decimal, int, int) CalculateOrderStatistics()",
                MethodCall = "var (totalPending, totalCompleted, pendingCount, completedCount) = CalculateOrderStatistics();"
            };
            
            var validationResult = MethodExtractionValidationResult.Success(methodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "ProcessOrders",
                RequiresReturnValue = true,
                SuggestedReturnType = "(decimal, decimal, int, int)",  // Tuple return type
                SuggestedParameters = [],
                LocalVariables = ["totalPending", "totalCompleted", "pendingCount", "completedCount"],
                ModifiedVariables = ["totalPending", "totalCompleted", "pendingCount", "completedCount"],
                HasComplexControlFlow = true,
                CyclomaticComplexity = 3
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Extraction should succeed. Error: {result.Error}");
            
            var change = result.Changes.First();
            
            // Should create method with tuple return
            Assert.Contains("private (decimal, decimal, int, int) CalculateOrderStatistics()", change.ModifiedContent);
            
            // Should return tuple of all modified variables
            Assert.Contains("return (totalPending, totalCompleted, pendingCount, completedCount);", change.ModifiedContent);
            
            // Should destructure tuple in method call
            Assert.Contains("var (totalPending, totalCompleted, pendingCount, completedCount) = CalculateOrderStatistics();", change.ModifiedContent);
        }

        [Fact]
        public async Task ExtractMethod_MultipleVariablesWithRefParameters_ShouldSucceed()
        {
            // Test scenario: Using ref parameters instead of tuple return for multiple modified variables
            
            var context = new RefactoringContext
            {
                FilePath = "Calculator.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class Calculator
    {
        public void PerformCalculations()
        {
            int sum = 0;
            int product = 1;
            int count = 0;
            
            // Extract this block that modifies existing variables
            for (int i = 1; i <= 10; i++)
            {
                sum += i;
                product *= i;
                count++;
            }
            
            Console.WriteLine($""Sum: {sum}, Product: {product}, Count: {count}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateValues",
                StartLine = 14,
                EndLine = 19,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup for ref parameter scenario
            var methodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private void CalculateValues(ref int sum, ref int product, ref int count)",
                MethodCall = "CalculateValues(ref sum, ref product, ref count);"
            };
            
            var validationResult = MethodExtractionValidationResult.Success(methodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "PerformCalculations",
                RequiresReturnValue = false,  // Use ref parameters instead
                SuggestedReturnType = "void",
                SuggestedParameters = ["ref int sum", "ref int product", "ref int count"],
                ModifiedVariables = ["sum", "product", "count"],
                LocalVariables = [],  // Variables are external, not local to extraction
                HasComplexControlFlow = false,
                CyclomaticComplexity = 2
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Extraction should succeed. Error: {result.Error}");
            
            var change = result.Changes.First();
            
            // Should create method with ref parameters
            Assert.Contains("private void CalculateValues(ref int sum, ref int product, ref int count)", change.ModifiedContent);
            
            // Should call method with ref parameters
            Assert.Contains("CalculateValues(ref sum, ref product, ref count);", change.ModifiedContent);
        }

        #endregion

        #region Complex Control Flow Tests

        [Fact]
        public async Task ExtractMethod_NestedLoopsWithAccumulators_ShouldHandleCorrectly()
        {
            // Test scenario: Nested loops with multiple accumulator variables
            
            var context = new RefactoringContext
            {
                FilePath = "MatrixProcessor.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class MatrixProcessor
    {
        public void ProcessMatrix()
        {
            int[,] matrix = new int[,] { {1,2,3}, {4,5,6}, {7,8,9} };
            
            // Extract nested loop calculation
            int sum = 0;
            int max = int.MinValue;
            int min = int.MaxValue;
            int elementCount = 0;
            
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int value = matrix[i, j];
                    sum += value;
                    if (value > max) max = value;
                    if (value < min) min = value;
                    elementCount++;
                }
            }
            
            double average = (double)sum / elementCount;
            
            Console.WriteLine($""Sum: {sum}, Average: {average:F2}"");
            Console.WriteLine($""Min: {min}, Max: {max}, Count: {elementCount}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateMatrixStatistics",
                StartLine = 12,
                EndLine = 27,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup for nested loop scenario
            var methodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private (int sum, int max, int min, int elementCount) CalculateMatrixStatistics(int[,] matrix)",
                MethodCall = "var (sum, max, min, elementCount) = CalculateMatrixStatistics(matrix);"
            };
            
            var validationResult = MethodExtractionValidationResult.Success(methodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "ProcessMatrix",
                RequiresReturnValue = true,
                SuggestedReturnType = "(int sum, int max, int min, int elementCount)",
                SuggestedParameters = ["int[,] matrix"],
                LocalVariables = ["sum", "max", "min", "elementCount"],
                ModifiedVariables = ["sum", "max", "min", "elementCount"],
                HasComplexControlFlow = true,
                CyclomaticComplexity = 5  // Nested loops with conditions
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Extraction should succeed. Error: {result.Error}");
            
            var change = result.Changes.First();
            
            // Should handle nested loops properly
            Assert.Contains("private (int sum, int max, int min, int elementCount) CalculateMatrixStatistics(int[,] matrix)", change.ModifiedContent);
            Assert.Contains("return (sum, max, min, elementCount);", change.ModifiedContent);
        }

        #endregion

        #region Variable Scope Analysis Tests

        [Fact]
        public async Task ExtractMethod_MixedLocalAndExternalVariables_ShouldHandleCorrectly()
        {
            // Test scenario: Mix of variables declared in extraction (local) and external variables
            
            var context = new RefactoringContext
            {
                FilePath = "DataAnalyzer.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;
using System.Collections.Generic;
using System.Linq;

namespace Analysis
{
    public class DataAnalyzer
    {
        public void AnalyzeData(List<int> data)
        {
            int threshold = 50;  // External variable
            int belowCount = 0;  // External variable
            
            // Extract this block - mix of local and external variables
            int aboveCount = 0;  // Local to extraction
            decimal sum = 0;     // Local to extraction
            
            foreach (var value in data)
            {
                if (value > threshold)
                {
                    aboveCount++;
                    sum += value;
                }
                else
                {
                    belowCount++;  // Modifies external variable
                }
            }
            
            decimal average = aboveCount > 0 ? sum / aboveCount : 0;
            
            Console.WriteLine($""Above threshold: {aboveCount} items, average: {average:F2}"");
            Console.WriteLine($""Below threshold: {belowCount} items"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "AnalyzeThreshold",
                StartLine = 15,
                EndLine = 30,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup for mixed variable scope scenario
            var methodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private (int aboveCount, decimal average, int belowCount) AnalyzeThreshold(List<int> data, int threshold, int belowCount)",
                MethodCall = "var (aboveCount, average, belowCount) = AnalyzeThreshold(data, threshold, belowCount);"
            };
            
            var validationResult = MethodExtractionValidationResult.Success(methodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "AnalyzeData",
                RequiresReturnValue = true,
                SuggestedReturnType = "(int aboveCount, decimal average, int belowCount)",
                SuggestedParameters = ["List<int> data", "int threshold", "int belowCount"],
                LocalVariables = ["aboveCount", "sum", "average"],  // Declared in extraction
                ModifiedVariables = ["aboveCount", "sum", "average", "belowCount"],
                HasComplexControlFlow = true,
                CyclomaticComplexity = 3
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Extraction should succeed. Error: {result.Error}");
            
            var change = result.Changes.First();
            
            // Should handle mixed variable scopes
            Assert.Contains("private (int aboveCount, decimal average, int belowCount) AnalyzeThreshold", change.ModifiedContent);
            
            // Local variables should be declared in method call
            Assert.Contains("var (aboveCount, average, belowCount) = AnalyzeThreshold(data, threshold, belowCount);", change.ModifiedContent);
        }

        #endregion

        #region LINQ and Lambda Expression Tests

        [Fact]
        public async Task ExtractMethod_LINQWithMultipleResults_ShouldHandleCorrectly()
        {
            // Test scenario: LINQ queries that produce multiple results
            
            var context = new RefactoringContext
            {
                FilePath = "LINQProcessor.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;
using System.Collections.Generic;
using System.Linq;

namespace DataProcessing
{
    public class LINQProcessor
    {
        public void ProcessData()
        {
            List<int> numbers = Enumerable.Range(1, 100).ToList();
            
            // Extract complex LINQ operations
            var evenNumbers = numbers.Where(n => n % 2 == 0).ToList();
            var oddNumbers = numbers.Where(n => n % 2 != 0).ToList();
            int evenSum = evenNumbers.Sum();
            int oddSum = oddNumbers.Sum();
            double evenAverage = evenNumbers.Average();
            double oddAverage = oddNumbers.Average();
            
            Console.WriteLine($""Even: Count={evenNumbers.Count}, Sum={evenSum}, Avg={evenAverage:F2}"");
            Console.WriteLine($""Odd: Count={oddNumbers.Count}, Sum={oddSum}, Avg={oddAverage:F2}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateStatsByParity",
                StartLine = 14,
                EndLine = 20,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup for LINQ scenario
            var methodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private (List<int> evenNumbers, List<int> oddNumbers, int evenSum, int oddSum, double evenAverage, double oddAverage) CalculateStatsByParity(List<int> numbers)",
                MethodCall = "var (evenNumbers, oddNumbers, evenSum, oddSum, evenAverage, oddAverage) = CalculateStatsByParity(numbers);"
            };
            
            var validationResult = MethodExtractionValidationResult.Success(methodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "ProcessData",
                RequiresReturnValue = true,
                SuggestedReturnType = "(List<int> evenNumbers, List<int> oddNumbers, int evenSum, int oddSum, double evenAverage, double oddAverage)",
                SuggestedParameters = ["List<int> numbers"],
                LocalVariables = ["evenNumbers", "oddNumbers", "evenSum", "oddSum", "evenAverage", "oddAverage"],
                ModifiedVariables = ["evenNumbers", "oddNumbers", "evenSum", "oddSum", "evenAverage", "oddAverage"],
                HasComplexControlFlow = false,
                CyclomaticComplexity = 1
            };
            
            validationResult.AddWarning("HIGH_VARIABLE_COUNT", "Extracting 6 variables may be complex. Consider a custom result class.");

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Extraction should succeed. Error: {result.Error}");
            Assert.Contains("warning", result.Message.ToLower());
            
            var change = result.Changes.First();
            
            // Should handle complex LINQ result extraction
            Assert.Contains("CalculateStatsByParity", change.ModifiedContent);
        }

        #endregion

        #region Exception Handling and Try-Catch Tests

        [Fact]
        public async Task ExtractMethod_TryCatchBlock_ShouldHandleCorrectly()
        {
            // Test scenario: Extracting code within or containing try-catch blocks
            
            var context = new RefactoringContext
            {
                FilePath = "ErrorHandler.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;
using System.IO;

namespace ErrorHandling
{
    public class FileProcessor
    {
        public void ProcessFile(string filePath)
        {
            string content = null;
            bool success = false;
            string errorMessage = null;
            
            // Extract try-catch block
            try
            {
                content = File.ReadAllText(filePath);
                success = true;
            }
            catch (FileNotFoundException ex)
            {
                errorMessage = $""File not found: {ex.Message}"";
                success = false;
            }
            catch (UnauthorizedAccessException ex)
            {
                errorMessage = $""Access denied: {ex.Message}"";
                success = false;
            }
            catch (Exception ex)
            {
                errorMessage = $""Unexpected error: {ex.Message}"";
                success = false;
            }
            
            if (success)
            {
                Console.WriteLine($""File content: {content}"");
            }
            else
            {
                Console.WriteLine($""Error: {errorMessage}"");
            }
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "TryReadFile",
                StartLine = 15,
                EndLine = 34,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup for try-catch scenario
            var methodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private (string content, bool success, string errorMessage) TryReadFile(string filePath)",
                MethodCall = "var (content, success, errorMessage) = TryReadFile(filePath);"
            };
            
            var validationResult = MethodExtractionValidationResult.Success(methodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "ProcessFile",
                RequiresReturnValue = true,
                SuggestedReturnType = "(string content, bool success, string errorMessage)",
                SuggestedParameters = ["string filePath"],
                LocalVariables = [],
                ModifiedVariables = ["content", "success", "errorMessage"],
                HasComplexControlFlow = true,
                CyclomaticComplexity = 4  // Multiple catch blocks
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Extraction should succeed. Error: {result.Error}");
            
            var change = result.Changes.First();
            
            // Should handle try-catch extraction
            Assert.Contains("private (string content, bool success, string errorMessage) TryReadFile(string filePath)", change.ModifiedContent);
            Assert.Contains("return (content, success, errorMessage);", change.ModifiedContent);
        }

        #endregion

        #region Async/Await Pattern Tests

        [Fact]
        public async Task ExtractMethod_AsyncCodeWithMultipleAwaits_ShouldHandleCorrectly()
        {
            // Test scenario: Extracting async code with multiple await statements
            
            var context = new RefactoringContext
            {
                FilePath = "AsyncProcessor.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AsyncProcessing
{
    public class WebDataFetcher
    {
        private readonly HttpClient _httpClient = new HttpClient();
        
        public async Task ProcessMultipleUrlsAsync()
        {
            string[] urls = { ""https://api1.example.com"", ""https://api2.example.com"" };
            
            // Extract async processing block
            string response1 = null;
            string response2 = null;
            int totalLength = 0;
            bool allSuccessful = true;
            
            try
            {
                response1 = await _httpClient.GetStringAsync(urls[0]);
                response2 = await _httpClient.GetStringAsync(urls[1]);
                totalLength = response1.Length + response2.Length;
            }
            catch (HttpRequestException)
            {
                allSuccessful = false;
            }
            
            Console.WriteLine($""Responses fetched: {allSuccessful}"");
            Console.WriteLine($""Total length: {totalLength}"");
            
            if (allSuccessful)
            {
                Console.WriteLine($""Response 1: {response1.Substring(0, Math.Min(100, response1.Length))}"");
                Console.WriteLine($""Response 2: {response2.Substring(0, Math.Min(100, response2.Length))}"");
            }
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "FetchResponsesAsync",
                StartLine = 16,
                EndLine = 30,
                AccessModifier = "private",
                IsStatic = false,
                ReturnType = "async Task<(string, string, int, bool)>"  // Async with tuple return
            };

            // Setup for async scenario
            var methodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private async Task<(string, string, int, bool)> FetchResponsesAsync(string[] urls)",
                MethodCall = "var (response1, response2, totalLength, allSuccessful) = await FetchResponsesAsync(urls);"
            };
            
            var validationResult = MethodExtractionValidationResult.Success(methodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "ProcessMultipleUrlsAsync",
                RequiresReturnValue = true,
                SuggestedReturnType = "async Task<(string, string, int, bool)>",
                SuggestedParameters = ["string[] urls"],
                LocalVariables = ["response1", "response2", "totalLength", "allSuccessful"],
                ModifiedVariables = ["response1", "response2", "totalLength", "allSuccessful"],
                HasComplexControlFlow = true,
                CyclomaticComplexity = 2
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Extraction should succeed. Error: {result.Error}");
            
            var change = result.Changes.First();
            
            // Should handle async method extraction
            Assert.Contains("private async Task<(string, string, int, bool)> FetchResponsesAsync(string[] urls)", change.ModifiedContent);
            Assert.Contains("return (response1, response2, totalLength, allSuccessful);", change.ModifiedContent);
            
            // Method call should await the async method
            Assert.Contains("var (response1, response2, totalLength, allSuccessful) = await FetchResponsesAsync(urls);", change.ModifiedContent);
        }

        #endregion

        #region Validation Error Scenarios

        [Fact]
        public async Task ExtractMethod_ComplexValidationFailure_ShouldProvideDetailedError()
        {
            // Test scenario: Complex validation failure with multiple issues
            
            var context = new RefactoringContext
            {
                FilePath = "ComplexValidator.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace Validation
{
    public class ComplexValidator
    {
        private int field1;
        private int field2;
        
        public void ComplexMethod()
        {
            int local1 = 10;
            int local2 = 20;
            
            // Try to extract code with multiple issues
            field1 = local1 + local2;
            field2 = field1 * 2;
            
            if (field1 > 100)
            {
                return;  // Early return in extraction
            }
            
            local1 = field2;
            goto skipSection;  // Goto statement
            
            local2 = 999;
            
            skipSection:
            Console.WriteLine(local1);
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "ExtractedMethod",
                StartLine = 16,
                EndLine = 29,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup for validation failure scenario
            var errors = new List<ValidationError>
            {
                new ValidationError("RETURN_STATEMENT", "Cannot extract code containing return statements that exit the containing method"),
                new ValidationError("GOTO_STATEMENT", "Cannot extract code containing goto statements"),
                new ValidationError("INSTANCE_FIELD_MODIFICATION", "Extraction modifies instance fields which may cause side effects"),
                new ValidationError("COMPLEX_CONTROL_FLOW", "Complex control flow makes extraction unsafe")
            };
            
            var validationResult = MethodExtractionValidationResult.Failure(errors, "Multiple validation errors detected");
            validationResult.AddWarning("HIGH_COMPLEXITY", "Code has high cyclomatic complexity (5)");
            validationResult.AddWarning("MULTIPLE_MODIFIED_VARIABLES", "Multiple variables are modified and used after extraction");
            
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "ComplexMethod",
                HasReturnStatements = true,
                HasComplexControlFlow = true,
                CyclomaticComplexity = 5
            };

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.False(result.Success, "Extraction should fail due to validation errors");
            Assert.NotNull(result.Error);
            Assert.Contains("return statements", result.Error);
            Assert.Contains("goto statements", result.Error);
            
            // Check metadata contains warnings
            Assert.Contains("validationWarnings", result.Metadata.Keys);
            Assert.Contains("cyclomatic complexity", result.Metadata["validationWarnings"].ToString());
        }

        #endregion

        #region Custom Result Class Scenario

        [Fact]
        public async Task ExtractMethod_SuggestCustomResultClass_ForManyVariables()
        {
            // Test scenario: When extracting many variables, suggest creating a custom result class
            
            var context = new RefactoringContext
            {
                FilePath = "ReportGenerator.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;
using System.Collections.Generic;
using System.Linq;

namespace Reporting
{
    public class ReportGenerator
    {
        public void GenerateReport(List<Sale> sales)
        {
            // Extract complex report calculation with many results
            decimal totalRevenue = sales.Sum(s => s.Amount);
            decimal averageRevenue = sales.Average(s => s.Amount);
            decimal maxSale = sales.Max(s => s.Amount);
            decimal minSale = sales.Min(s => s.Amount);
            int totalCount = sales.Count;
            int highValueCount = sales.Count(s => s.Amount > 1000);
            int mediumValueCount = sales.Count(s => s.Amount >= 500 && s.Amount <= 1000);
            int lowValueCount = sales.Count(s => s.Amount < 500);
            DateTime earliestDate = sales.Min(s => s.Date);
            DateTime latestDate = sales.Max(s => s.Date);
            
            // Use all calculated values
            Console.WriteLine($""Total Revenue: ${totalRevenue:F2}"");
            Console.WriteLine($""Average: ${averageRevenue:F2}, Max: ${maxSale:F2}, Min: ${minSale:F2}"");
            Console.WriteLine($""Count - Total: {totalCount}, High: {highValueCount}, Medium: {mediumValueCount}, Low: {lowValueCount}"");
            Console.WriteLine($""Date Range: {earliestDate:d} to {latestDate:d}"");
        }
    }
    
    public class Sale
    {
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateReportStatistics",
                StartLine = 12,
                EndLine = 21,
                AccessModifier = "private",
                IsStatic = false
            };

            // Setup for many variables scenario
            var methodInfo = new ExtractedMethodInfo
            {
                MethodSignature = "private ReportStatistics CalculateReportStatistics(List<Sale> sales)",
                MethodCall = "var stats = CalculateReportStatistics(sales);"
            };
            
            var validationResult = MethodExtractionValidationResult.Success(methodInfo);
            validationResult.Analysis = new CSharpExtractionAnalysis
            {
                ContainingMethodName = "GenerateReport",
                RequiresReturnValue = true,
                SuggestedReturnType = "ReportStatistics",  // Suggest custom class
                SuggestedParameters = ["List<Sale> sales"],
                LocalVariables =
                [
                    "totalRevenue", "averageRevenue", "maxSale", "minSale",
                    "totalCount", "highValueCount", "mediumValueCount", "lowValueCount",
                    "earliestDate", "latestDate"
                ],
                ModifiedVariables =
                [
                    "totalRevenue", "averageRevenue", "maxSale", "minSale",
                    "totalCount", "highValueCount", "mediumValueCount", "lowValueCount",
                    "earliestDate", "latestDate"
                ],
                HasComplexControlFlow = false,
                CyclomaticComplexity = 1
            };
            
            validationResult.AddWarning("HIGH_VARIABLE_COUNT", "Extracting 10 variables is highly complex. Consider creating a ReportStatistics class to encapsulate the results.");
            validationResult.AddWarning("TUPLE_COMPLEXITY", "Tuple with 10 elements would be difficult to maintain. Strongly recommend a custom result class.");

            _mockValidator.Setup(v => v.ValidateExtractionAsync(
                It.IsAny<string>(),
                It.IsAny<ExtractMethodOptions>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(validationResult);

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Extraction should succeed with warnings. Error: {result.Error}");
            Assert.Contains("warning", result.Message.ToLower());
            
            // Check for custom class suggestion in warnings
            Assert.Contains("validationWarnings", result.Metadata.Keys);
            var warnings = result.Metadata["validationWarnings"].ToString();
            Assert.Contains("ReportStatistics", warnings);
            Assert.Contains("custom result class", warnings.ToLower());
        }

        #endregion
    }
}
